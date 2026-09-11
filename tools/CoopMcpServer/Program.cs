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
builder.Services.AddTransient<IOwnedProcessFactory, OwnedProcessFactory>();
builder.Services.AddTransient<ISaveDirectoryProvider, SaveDirectoryProvider>();
builder.Services.AddTransient<ISaveCatalog, SaveCatalog>();
builder.Services.AddTransient<IMemoryCommitProbe, MemoryCommitProbe>();
builder.Services.AddTransient<IBridgeBuildInspector, BridgeBuildInspector>();
builder.Services.AddTransient<ILaunchPreflight, LaunchPreflight>();
builder.Services.AddTransient<IGameProcessLauncher, InGameProcessLauncher>();
builder.Services.AddTransient<ILiveTestPipeClient, LiveTestPipeClient>();
builder.Services.AddTransient<IIncrementalLogReader, IncrementalLogReader>();
// Run ownership must outlive individual MCP tool invocations.
builder.Services.AddSingleton<RunOrchestrator>();
builder.Services.AddSingleton<IRunOrchestrator>(services => services.GetRequiredService<RunOrchestrator>());
builder.Services.AddSingleton<IDeploymentRunGuard>(services => services.GetRequiredService<RunOrchestrator>());
builder.Services.AddTransient<IDeploymentPaths, DeploymentPaths>();
builder.Services.AddTransient<IDeploymentLease, DeploymentLease>();
builder.Services.AddTransient<IDeploymentEnvironment, DeploymentEnvironment>();
builder.Services.AddTransient<IDeploymentPlan, DeploymentPlan>();
builder.Services.AddTransient<IDeploymentFiles, DeploymentFiles>();
builder.Services.AddTransient<IModBuildService, ModBuildService>();
builder.Services.AddTransient<IModDeploymentService, ModDeploymentService>();
builder.Services.AddTransient<IDeploymentTools, DeploymentTools>();
builder.Services.AddTransient<IScreenshotImageEncoder, ScreenshotImageEncoder>();
builder.Services.AddTransient<IScreenshotCapture, ScreenshotCapture>();
builder.Services.AddTransient<IDebugTools, DebugTools>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<DebugTools>().WithTools<DeploymentTools>();
using var host = builder.Build();
var runs = host.Services.GetRequiredService<IRunOrchestrator>();
try
{
    await host.StartAsync();
    await host.WaitForShutdownAsync();
}
finally { await runs.StopAllAsync(); }
