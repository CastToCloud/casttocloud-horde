using System.Globalization;
using System.Text;
using BetterConsoleTables;
using EpicGames.Core;
using EpicGames.Horde.Jobs.Schedules;
using EpicGames.Horde.Storage;
using HordeServer.Server;
using HordeServer.Storage;
using HordeServer.Utilities;
using CastToCloud.Horde.StorageReporter.Senders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CastToCloud.Horde.StorageReporter;

/// <summary>
/// Persisted state for the storage reporter: when we last fired a report.
/// </summary>
[SingletonDocument("storage-reporter-state")]
internal class StorageReporterState : SingletonBase
{
	public DateTime LastReportTimeUtc { get; set; }
}

/// <summary>
/// Periodically gathers storage stats and dispatches a formatted report to every configured sender.
/// </summary>
public sealed class StorageReportService : IHostedService, IAsyncDisposable
{
	private readonly IReadOnlyList<IStorageReportSender> _senders;
	private readonly IOptionsMonitor<StorageReporterConfig> _config;
	private readonly SingletonDocument<StorageReporterState> _state;

	private readonly ITicker _ticker;
	private readonly IClock _clock;
	private readonly StorageService _storageService;
	private readonly ILogger<StorageReportService> _logger;

	public StorageReportService(
		IEnumerable<IStorageReportSender> senders,
		IOptionsMonitor<StorageReporterConfig> config,
		StorageService storageService,
		ILogger<StorageReportService> logger,
		IClock clock,
		IMongoService mongoService)
	{
		_senders = senders.ToList();
		_config = config;
		_state = new SingletonDocument<StorageReporterState>(mongoService);

		_ticker = clock.AddSharedTicker<StorageReportService>(TimeSpan.FromMinutes(1.0), TickAsync, logger);
		_clock = clock;
		_storageService = storageService;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
		=> _ticker.StartAsync();

	public Task StopAsync(CancellationToken cancellationToken)
		=> _ticker.StopAsync();

	public ValueTask DisposeAsync()
		=> _ticker.DisposeAsync();

	internal async ValueTask TickAsync(CancellationToken cancellationToken)
	{
		StorageReporterState state = await _state.GetAsync(cancellationToken);
		DateTime currentTime = _clock.UtcNow;

		if (state.LastReportTimeUtc == default)
		{
			// First run: skip past any already-elapsed scheduled occurrences.
			await _state.UpdateAsync(s => s.LastReportTimeUtc = currentTime, cancellationToken);
			return;
		}

		DateTime nextScheduledTimeUtc = _config.CurrentValue.Schedule.GetNextTriggerTimeUtc(state.LastReportTimeUtc, _clock.TimeZone);
		if (nextScheduledTimeUtc > currentTime)
		{
			return;
		}
		await _state.UpdateAsync(s => s.LastReportTimeUtc = nextScheduledTimeUtc, cancellationToken);

		IReadOnlyList<IStorageStats> storageStats = await _storageService.FindStatsAsync(count: 1, cancellationToken: cancellationToken);
		IStorageStats? stats = storageStats.FirstOrDefault();
		if (stats == null)
		{
			_logger.LogInformation("No stats available yet, skipping report.");
			return;
		}

		string report = FormatReport(stats);

		_logger.LogInformation("Dispatching storage report ({Namespaces} namespaces) to {Senders} senders:\n{Report}", stats.Namespaces.Count, _senders.Count, report);

		foreach (IStorageReportSender sender in _senders)
		{
			try
			{
				_logger.LogInformation("Dispatching report via {Sender}.", sender.Name);
				await sender.SendAsync(report, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Sender {Sender} failed to send report.", sender.Name);
			}
		}
	}

	internal static string FormatReport(IStorageStats stats)
	{
		var sortedNamespaces = stats.Namespaces
			.OrderByDescending(kvp => kvp.Value.Size)
			.ToList();

		long totalBytes = stats.Namespaces.Sum(kvp => kvp.Value.Size);
		long totalCount = stats.Namespaces.Sum(kvp => kvp.Value.Count);

		Table table = new Table(
			new ColumnHeader("Namespace", Alignment.Center, Alignment.Left),
			new ColumnHeader("Size", Alignment.Center, Alignment.Right),
			new ColumnHeader("Blobs", Alignment.Center, Alignment.Right));

		foreach (var (namespaceId, ns) in sortedNamespaces)
		{
			table.AddRow(
				namespaceId.ToString(),
				StringUtils.FormatBytesString(ns.Size),
				ns.Count.ToString("N0", CultureInfo.InvariantCulture));
		}

		StringBuilder sb = new StringBuilder();
		sb.AppendLine(CultureInfo.InvariantCulture, $"*Horde storage report* — snapshot {stats.Time:u}");
		sb.AppendLine(CultureInfo.InvariantCulture, $"Total: *{StringUtils.FormatBytesString(totalBytes)}* across *{totalCount:N0}* blobs in *{stats.Namespaces.Count}* namespaces.");
		sb.AppendLine();
		sb.AppendLine("```");
		sb.Append(table.ToString());
		sb.AppendLine("```");
		return sb.ToString();
	}
}
