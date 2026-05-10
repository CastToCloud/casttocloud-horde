using System.Net.Http.Headers;
using EpicGames.Slack;
using HordeServer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CastToCloud.Horde.StorageReporter.Senders;

/// <summary>
/// Slack delivery details for the storage report. Token is reused from <c>BuildServerConfig.SlackToken</c>.
/// </summary>
public class StorageReporterSlackConfig
{
	/// <summary>
	/// Channel name (#disk-usage), channel ID (C0123ABC), or user ID for DM (U0123ABC).
	/// </summary>
	public string? Destination { get; set; }
}

internal sealed class SlackSender : IStorageReportSender
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IOptions<BuildServerConfig> _buildServerConfig;
	private readonly IOptionsMonitor<StorageReporterConfig> _config;
	private readonly ILogger<SlackSender> _logger;

	public string Name => "Slack";

	public SlackSender(
		IHttpClientFactory httpClientFactory,
		IOptions<BuildServerConfig> buildServerConfig,
		IOptionsMonitor<StorageReporterConfig> config,
		ILogger<SlackSender> logger)
	{
		_httpClientFactory = httpClientFactory;
		_buildServerConfig = buildServerConfig;
		_config = config;
		_logger = logger;
	}

	public async Task SendAsync(string report, CancellationToken cancellationToken)
	{
		string? token = _buildServerConfig.Value.SlackToken;
		if (string.IsNullOrEmpty(token))
		{
			_logger.LogWarning("Slack send skipped: SlackToken is not set in BuildServerConfig.");
			return;
		}

		string? destination = _config.CurrentValue.Slack?.Destination;
		if (string.IsNullOrEmpty(destination))
		{
			_logger.LogWarning("Slack send skipped: Destination is not set in StorageReporter global config.");
			return;
		}

		using HttpClient httpClient = _httpClientFactory.CreateClient();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		SlackClient slackClient = new SlackClient(httpClient, _logger);
		await slackClient.PostMessageAsync(destination, report, cancellationToken);
	}
}
