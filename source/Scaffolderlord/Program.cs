using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Scaffolderlord.CLI.Commands;
using Scaffolderlord.Models;
using Microsoft.Extensions.DependencyInjection;
using RazorLight;
using DotMake.CommandLine;
using Scaffolderlord.Services;
using Scaffolderlord.CLI;
using Scaffolderlord.Services.Impl;
using Microsoft.Extensions.Logging;

namespace Scaffolderlord
{
	class Program
	{
		static async Task<int> Main(string[] args)
		{
			InitializeDI();
			return await Cli.RunAsync<RootCliCommand>(args);
		}

		private static void InitializeDI()
		{
			Cli.Ext.ConfigureServices(services =>
			{
				services.AddSingleton<IRazorLightEngine>(provider =>
					new RazorLightEngineBuilder()
						.DisableEncoding()
						.UseMemoryCachingProvider()
						.Build())
				.AddTransient<IScaffoldingService, ScaffoldingService>()
				.AddLogging(builder => builder.AddProvider(new LoggerProvider()));
			});
		}

	}
}
