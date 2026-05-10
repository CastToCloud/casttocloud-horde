using CastToCloud.Horde.StorageReporter.Senders;
using EpicGames.Horde.Jobs.Schedules;
using HordeServer.Jobs.Schedules;
using HordeServer.Plugins;

namespace CastToCloud.Horde.StorageReporter;

/// <summary>
/// Storage reporter global (hot-reloadable) settings, under <c>Plugins.StorageReporter</c> in the global config file.
/// </summary>
public class StorageReporterConfig : IPluginConfig
{
	/// <summary>
	/// When to send the report. Defaults to 09:00 every day.
	/// </summary>
	public SchedulePatternConfig Schedule { get; set; } = new SchedulePatternConfig
	{
		MinTime = new ScheduleTimeOfDay(9 * 60),
	};

	/// <summary>
	/// Slack delivery configuration.
	/// </summary>
	public StorageReporterSlackConfig? Slack { get; set; }

	public void PostLoad(PluginConfigOptions configOptions)
	{
	}
}
