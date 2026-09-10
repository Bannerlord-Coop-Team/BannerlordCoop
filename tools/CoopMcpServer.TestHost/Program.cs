using CoopMcpServer;
using CoopMcpServer.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Harmless protocol fixtures: no OS game launcher, game dependencies, or registration access.
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
IRunOrchestrator runs = args.Length == 3 && args[1] == "launch-schema"
    ? new LaunchSchemaFixture(args[0], args[2]).CreateOrchestrator()
    : new ScreenshotRunFixture(args[0]) { FailureMethod = args.Length == 2 ? args[1] : null };
builder.Services.AddSingleton(runs);
builder.Services.AddTransient<IScreenshotImageEncoder, ScreenshotImageEncoder>();
builder.Services.AddTransient<IScreenshotCapture, ScreenshotCapture>();
builder.Services.AddTransient<IDebugTools, DebugTools>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<DebugTools>();
using var host = builder.Build();
await host.RunAsync();
