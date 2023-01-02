using System.Net;
using System.Reflection;
using IntroServer.Config;
using IntroServer.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace ServerConsole
{
    internal class Program
    {
        private const int TicksPerSecond = 120;
        private static readonly CancellationTokenSource ServerCancellation = new();
        private static MissionTestServer? TestServer;
        private static Task? PollTask;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static async Task Main()
        {
            try
            {
                var logTarget = new ColoredConsoleTarget
                {
	                Layout = "${date:format=HH\\:MM\\:ss} [${logger}] : ${message} ${exception}",
                };
                NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(logTarget, LogLevel.Trace);
                Logger.Trace("Building Network Configuration");
                var configurationBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddUserSecrets<Program>();
    
                var config = configurationBuilder
                    .Build()
                    .Get<NetworkConfiguration>(options => 
                        options.BindNonPublicProperties = true)!;

                await using var provider = new ServiceCollection()
                    .AddTransient<MissionTestServer>()
                    .AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddNLog();
                    })
                    .AddSingleton(config)
                    .BuildServiceProvider();
                
                TestServer = provider.GetRequiredService<MissionTestServer>();

                Logger.Info(config.NATType == NATType.Internal
                    ? $"Server started on port: {config.LanPort}"
                    : $"Server started on port: {config.WanPort}");

                Logger.Warn($"Type STOP to stop the server.");
                PollTask = Task.Factory
                    .StartNew(PollServer, ServerCancellation.Token);

                while (Console.ReadLine() != "STOP")
                {
                }

                ServerCancellation.Cancel();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Error occurred while trying to run server");
                return;
            }

            try
            {
                await PollTask;
            }
            catch (TaskCanceledException)
            {
                // expected
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to gracefully stop server");
            }
        }

        private static async void PollServer()
        {
            Logger.Trace("Started Update Polling Loop");
            while (!ServerCancellation.IsCancellationRequested)
            {
                TestServer?.Update();
                await Task.Delay(1000 / TicksPerSecond);
            }
        }
    }
}