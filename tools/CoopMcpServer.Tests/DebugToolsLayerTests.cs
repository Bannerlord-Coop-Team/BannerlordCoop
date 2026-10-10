using Json.Schema;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed class DebugToolsLayerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CoopLayerSdk-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(40));

    public DebugToolsLayerTests() => Directory.CreateDirectory(directory);

    [Theory]
    [InlineData("ui_layers", "ui-ready", null)]
    [InlineData("ui_layers", "ui-missing", "ui_capability_unavailable")]
    [InlineData("ui_layers", "ui-wrong", "ui_capability_unavailable")]
    [InlineData("ui_layers", "ui-status-failure", "game_thread_timeout")]
    [InlineData("ui_inspect", "ui-ready", null)]
    [InlineData("ui_inspect", "ui-missing", "ui_capability_unavailable")]
    [InlineData("ui_inspect", "ui-wrong", "ui_capability_unavailable")]
    [InlineData("ui_inspect", "ui-status-failure", "game_thread_timeout")]
    public async Task SdkDispatchPreservesLayerCapabilitySuccessAndFailure(string tool, string scenario, string error)
    {
        await using var client = await Connect(scenario);
        var run = await Call(client, "start_run", new() { ["profile"] = "fixture", ["client_count"] = 1 });
        string runId = run.GetProperty("runId").GetString();
        var args = new Dictionary<string, object> { ["run_id"] = runId, ["instance"] = "client1" };
        string layer = new string('a', 32);
        if (tool == "ui_inspect") args["layer"] = layer;
        var result = await Call(client, tool, args);
        Assert.Equal(error == null, result.GetProperty("ok").GetBoolean());
        if (error != null)
        {
            Assert.Equal(error, result.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal(scenario == "ui-status-failure", result.GetProperty("error").GetProperty("outcomeUncertain").GetBoolean());
            if (scenario == "ui-status-failure") Assert.Equal("exact-status-response", result.GetProperty("id").GetString());
        }
        else
        {
            var dispatch = result.GetProperty("result");
            Assert.Equal(tool.Replace('_', '-'), dispatch.GetProperty("method").GetString());
            Assert.Equal(new[] { "status", tool.Replace('_', '-') }, dispatch.GetProperty("methods").EnumerateArray().Select(x => x.GetString()));
            Assert.False(dispatch.GetProperty("mutation").GetBoolean());
            if (tool == "ui_inspect")
            {
                var payload = dispatch.GetProperty("parameters");
                Assert.Equal(layer, payload.GetProperty("layer").GetString());
                Assert.Equal(0, payload.GetProperty("offset").GetInt32());
                Assert.Equal(JsonValueKind.Null, payload.GetProperty("snapshot").ValueKind);
            }
        }
        // A legacy request still reaches the pipe without a capability probe or a synthetic layer field.
        args.Remove("layer");
        var legacy = (await Call(client, "ui_inspect", args)).GetProperty("result");
        Assert.False(legacy.GetProperty("parameters").TryGetProperty("layer", out _));
        Assert.Equal(new[] { "status", "ui-inspect" }.Length + (error == null ? 1 : 0), legacy.GetProperty("methods").GetArrayLength());
        await Call(client, "stop_run", new() { ["run_id"] = runId });
    }

    private Task<McpClient> Connect(string scenario) => McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Harmless layer protocol fixture", Command = Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe"),
        Arguments = new[] { directory, "launch-schema", scenario }, ShutdownTimeout = TimeSpan.FromSeconds(10),
    }), cancellationToken: timeout.Token);

    private async Task<JsonElement> Call(McpClient client, string name, Dictionary<string, object> args)
    {
        var tool = (await client.ListToolsAsync(cancellationToken: timeout.Token)).Single(t => t.Name == name);
        var response = await client.CallToolAsync(name, args, cancellationToken: timeout.Token);
        Assert.NotEqual(true, response.IsError);
        Assert.True(response.StructuredContent.HasValue);
        Assert.True(tool.ReturnJsonSchema.HasValue);
        var value = response.StructuredContent.Value;
        Assert.True(JsonSchema.FromText(tool.ReturnJsonSchema.Value.GetRawText()).Evaluate(value).IsValid, name);
        return value;
    }

    public void Dispose()
    {
        timeout.Dispose();
        Directory.Delete(directory, true);
    }
}
