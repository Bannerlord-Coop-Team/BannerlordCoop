using IntroServer.Config;
using IntroServer.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace ServerConsole
{
    internal class Program
    {
        private const int TicksPerSecond = 120;
        private static readonly CancellationTokenSource ServerCancellation = new();
        private static MissionTestServer? TestServer;
        private static Task? PollTask;
        private static ILogger? Logger;

        private static async Task Main()
		{
			Logger = new LoggerConfiguration()
				.WriteTo.Console(
					outputTemplate:
					"[{Timestamp:HH:mm:ss} {Level:u3} ({SourceContext})] {Message:lj}{NewLine}{Exception}")
				.MinimumLevel.Verbose()
				.CreateLogger()
				.ForContext<Program>();
			try
            {
                Logger.Verbose("Building Network Configuration");
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
                        builder.AddSerilog();
                    })
                    .AddSingleton(config)
                    .BuildServiceProvider();
                
                TestServer = provider.GetRequiredService<MissionTestServer>();

                Logger.Information(config.NATType == NATType.Internal
                    ? $"Server started on port: {config.LanPort}"
                    : $"Server started on port: {config.WanPort}");

                Logger.Warning("Type STOP to stop the server.");
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
            Logger?.Verbose("Started Update Polling Loop");
            while (!ServerCancellation.IsCancellationRequested)
            {
                TestServer?.Update();
                await Task.Delay(1000 / TicksPerSecond);
            }
        }
    }
}