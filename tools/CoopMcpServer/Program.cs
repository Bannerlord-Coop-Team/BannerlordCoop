using CoopMcpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("CoopMcpServer requires Windows.");
if (args.Length != 2 || args[0] != "--config")
    throw new ArgumentException("Usage: CoopMcpServer --config C:\\absolute\\profiles.json");
var settings = CoopMcpServerSettings.Load(args[1]);
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(settings);
builder.Services.AddTransient<IGameProcessLauncher, InGameProcessLauncher>();
builder.Services.AddTransient<ILiveTestPipeClient, LiveTestPipeClient>();
builder.Services.AddTransient<IIncrementalLogReader, IncrementalLogReader>();
// Run ownership must outlive individual MCP tool invocations.
builder.Services.AddSingleton<IRunOrchestrator, RunOrchestrator>();
builder.Services.AddTransient<IDebugTools, DebugTools>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<DebugTools>();
using var host = builder.Build();
var runs = host.Services.GetRequiredService<IRunOrchestrator>();
try
{
    await host.StartAsync();
    await host.WaitForShutdownAsync();
}
finally { await runs.StopAllAsync(); }
