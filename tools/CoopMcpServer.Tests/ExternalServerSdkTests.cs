using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CoopMcpServer.Tests;

// Drives an external-server profile through the official SDK, the only place to see what an MCP client actually receives.
public sealed class ExternalServerSdkTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CoopExternalSdk-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(40));

    public ExternalServerSdkTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task ServerInstanceFailsWithAMessageTheClientCanRead()
    {
        await using var client = await Connect();
        string runId = await StartEmptyRun(client);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        var args = new Dictionary<string, object>
        {
            ["run_id"] = runId, ["instance"] = "server", ["state"] = "readyForCampaignTests", ["timeout_seconds"] = 1,
            ["name"] = "coop.debug.town.list_towns", ["arguments"] = Array.Empty<string>(),
        };
        foreach (string tool in new[] { "wait_for_state", "list_commands", "execute_command", "join_client", "read_logs", "screenshot", "capture_screenshot" })
        {
            var parameters = tools.Single(t => t.Name == tool).JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();
            var result = await client.CallToolAsync(tool, args.Where(a => parameters.Contains(a.Key)).ToDictionary(a => a.Key, a => a.Value),
                cancellationToken: timeout.Token);
            Assert.True(result.IsError, tool);
            Assert.Contains("MCP does not own it", Text(result));
        }
    }

    [Fact]
    public async Task SaveNameFailsWithAMessageTheClientCanRead()
    {
        await using var client = await Connect();
        var result = await client.CallToolAsync("start_run", new Dictionary<string, object> { ["profile"] = "fixture", ["client_count"] = 0, ["save_name"] = "Danustica campaign" },
            cancellationToken: timeout.Token);
        Assert.True(result.IsError);
        Assert.Contains("external dedicated server", Text(result));
    }

    [Fact]
    public async Task ClientWorkflowRunsWithoutAServerAndStopsClean()
    {
        await using var client = await Connect();
        string runId = await StartEmptyRun(client);
        var added = await client.CallToolAsync("start_client", new Dictionary<string, object> { ["run_id"] = runId, ["client_index"] = 1 }, cancellationToken: timeout.Token);
        Assert.Equal("launched", added.StructuredContent.Value.GetProperty("outcome").GetString());
        var wait = await client.CallToolAsync("wait_for_state", new Dictionary<string, object>
        {
            ["run_id"] = runId, ["instance"] = "client1", ["state"] = "controlReady", ["timeout_seconds"] = 5,
        }, cancellationToken: timeout.Token);
        Assert.True(wait.StructuredContent.Value.GetProperty("result").GetProperty("reached").GetBoolean());
        var stopped = await client.CallToolAsync("stop_run", new Dictionary<string, object> { ["run_id"] = runId }, cancellationToken: timeout.Token);
        Assert.Equal("stopped", stopped.StructuredContent.Value.GetProperty("state").GetString());
        var instance = Assert.Single(stopped.StructuredContent.Value.GetProperty("instances").EnumerateArray());
        Assert.Equal("client1", instance.GetProperty("name").GetString());
        Assert.True(instance.GetProperty("cleanupComplete").GetBoolean());
    }

    // Connects to the harmless fixture host configured with an external-server profile.
    private Task<McpClient> Connect() => McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Harmless external server fixture", Command = Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe"),
        Arguments = new[] { directory, "launch-schema", "external-server" }, ShutdownTimeout = TimeSpan.FromSeconds(10),
    }), cancellationToken: timeout.Token);

    // Starts a run with no clients and returns its id, checking that it launched nothing.
    private async Task<string> StartEmptyRun(McpClient client)
    {
        var run = await client.CallToolAsync("start_run", new Dictionary<string, object> { ["profile"] = "fixture", ["client_count"] = 0 }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, run.IsError);
        Assert.Empty(run.StructuredContent.Value.GetProperty("instances").EnumerateArray());
        return run.StructuredContent.Value.GetProperty("runId").GetString();
    }

    // Joins the text blocks of a tool result, which is all an MCP client sees of a failed call.
    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    public void Dispose()
    {
        timeout.Dispose();
        Directory.Delete(directory, true);
    }
}
