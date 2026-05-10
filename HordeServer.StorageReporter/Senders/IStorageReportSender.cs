namespace CastToCloud.Horde.StorageReporter.Senders;

/// <summary>
/// A delivery channel for the storage report (Slack, Discord, Email, ...).
/// </summary>
internal interface IStorageReportSender
{
	/// <summary>
	/// Short name used in log messages (e.g., "Slack", "Discord").
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Send a formatted report.
	/// </summary>
	Task SendAsync(string report, CancellationToken cancellationToken);
}
