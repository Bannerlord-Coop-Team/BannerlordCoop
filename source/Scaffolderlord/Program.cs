using static Scaffolderlord.Extensions;
using Microsoft.Extensions.DependencyInjection;
using RazorLight;
using DotMake.CommandLine;
using Scaffolderlord.Services;
using Scaffolderlord.CLI;
using Scaffolderlord.Services.Impl;
using Microsoft.Extensions.Logging;
using TaleWorlds.Engine;
using Scaffolderlord.Helpers;

namespace Scaffolderlord
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            ConfigureServices();
            CheckIrregularPath();
            return await Cli.RunAsync<RootCliCommand>(args);
        }

        private static void ConfigureServices()
        {
            Cli.Ext.ConfigureServices(services =>
            {
                services.AddSingleton<IRazorLightEngine>(provider =>
                    new RazorLightEngineBuilder()
                        .DisableEncoding()
                        .UseMemoryCachingProvider()
                        .Build())
                        .AddTransient<IScaffoldingService, RazorlightScaffolder>()
                        .AddLogging(builder => builder
                        .SetMinimumLevel(LogLevel.Debug)
                        .AddProvider(new LoggerProvider())
                ).PropagateLogger();
            });
        }

        private static void CheckIrregularPath()
        {
            _ = BannerlordCoopProjectRoot;
        }

    }
}
