using HordeServer.Plugins;
using CastToCloud.Horde.StorageReporter.Senders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CastToCloud.Horde.StorageReporter;

[Plugin("StorageReporter", GlobalConfigType = typeof(StorageReporterConfig), DependsOn = ["Build"])]
public class StorageReporterPlugin : IPluginStartup
{
	public void Configure(IApplicationBuilder app)
	{
	}

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddHttpClient();
		services.AddHostedService<StorageReportService>();
		services.AddSingleton<IStorageReportSender, SlackSender>();
	}
}
