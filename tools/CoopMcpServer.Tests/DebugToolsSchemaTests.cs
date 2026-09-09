using Json.Schema;
using ModelContextProtocol.Client;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CoopMcpServer.Tests;

public sealed class DebugToolsSchemaTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CoopSchemaTest-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(40));

    public DebugToolsSchemaTests() => Directory.CreateDirectory(directory);

    [Theory]
    [InlineData("missing_markers", "bridge_build_incompatible")]
    [InlineData("wrong_protocol", "bridge_build_incompatible")]
    [InlineData("bridge_build_unavailable", "bridge_build_unavailable")]
    [InlineData("commit_probe_unavailable", "commit_probe_unavailable")]
    [InlineData("commit_probe_invalid", "commit_probe_invalid")]
    [InlineData("commit_headroom_insufficient", "commit_headroom_insufficient")]
    [InlineData("ready", "ready")]
    public async Task PreflightStructuredResponseMatchesAdvertisedOutputSchema(string scenario, string code)
    {
        await using var client = await Connect(scenario);
        var report = await CallAndValidate(client, "preflight_run", new() { ["profile"] = "fixture", ["client_count"] = 1 });
        Assert.Equal(code, report.GetProperty("code").GetString());
        Assert.Equal(code == "ready", report.GetProperty("allowed").GetBoolean());
        Assert.False(string.IsNullOrEmpty(report.GetProperty("message").GetString()));
        if (scenario == "missing_markers")
        {
            Assert.Equal(JsonValueKind.Null, report.GetProperty("bridge").GetProperty("protocol").ValueKind);
            Assert.Equal(JsonValueKind.Null, report.GetProperty("bridge").GetProperty("capabilities").ValueKind);
        }
        if (scenario == "commit_probe_unavailable") Assert.Equal(JsonValueKind.Null, report.GetProperty("commit").ValueKind);
        if (scenario.StartsWith("commit_", StringComparison.Ordinal) || scenario == "bridge_build_unavailable")
            Assert.Equal(JsonValueKind.Null, report.GetProperty("bridge").ValueKind);
    }

    [Theory]
    [InlineData("missing_markers", "preflight_failed")]
    [InlineData("wrong_protocol", "preflight_failed")]
    [InlineData("bridge_build_unavailable", "preflight_failed")]
    [InlineData("commit_probe_unavailable", "preflight_failed")]
    [InlineData("commit_probe_invalid", "preflight_failed")]
    [InlineData("commit_headroom_insufficient", "preflight_failed")]
    [InlineData("launch_failed", "launch_failed")]
    [InlineData("ownership_probe_failed", "started")]
    [InlineData("ready", "started")]
    public async Task StartAndStopStructuredResponsesMatchAdvertisedOutputSchema(string scenario, string state)
    {
        await using var client = await Connect(scenario);
        var run = await CallAndValidate(client, "start_run", new() { ["profile"] = "fixture", ["client_count"] = 0 });
        Assert.Equal(state, run.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("requestedSave").ValueKind);
        if (state != "started") Assert.Empty(run.GetProperty("instances").EnumerateArray());
        else
        {
            Assert.Equal(JsonValueKind.Null, run.GetProperty("error").ValueKind);
            var instance = run.GetProperty("instances")[0];
            Assert.Equal(JsonValueKind.Null, instance.GetProperty("status").ValueKind);
            if (scenario == "ownership_probe_failed")
            {
                Assert.Equal(JsonValueKind.Null, instance.GetProperty("processTreeAlive").ValueKind);
                Assert.Equal("ownership_probe_failed", instance.GetProperty("error").GetProperty("code").GetString());
            }
            else Assert.Equal(JsonValueKind.Null, instance.GetProperty("error").ValueKind);
        }
        var args = new Dictionary<string, object> { ["run_id"] = run.GetProperty("runId").GetString() };
        await CallAndValidate(client, "get_run", args);
        var stopped = await CallAndValidate(client, "stop_run", args);
        Assert.Equal("stopped", stopped.GetProperty("state").GetString());
    }

    [Theory]
    [InlineData("missing_markers", "preflight_rejected", "bridge_build_incompatible")]
    [InlineData("wrong_protocol", "preflight_rejected", "bridge_build_incompatible")]
    [InlineData("bridge_build_unavailable", "preflight_rejected", "bridge_build_unavailable")]
    [InlineData("commit_probe_unavailable", "preflight_rejected", "commit_probe_unavailable")]
    [InlineData("commit_probe_invalid", "preflight_rejected", "commit_probe_invalid")]
    [InlineData("commit_headroom_insufficient", "preflight_rejected", "commit_headroom_insufficient")]
    [InlineData("launch_failed", "launch_failed", "client_launch_failed")]
    [InlineData("ready", "launched", null)]
    public async Task StagedClientFailuresAndRetriesMatchAdvertisedOutputSchema(string scenario, string outcome, string code)
    {
        await using var client = await Connect("staged-" + scenario);
        var run = await CallAndValidate(client, "start_run", new() { ["profile"] = "fixture", ["client_count"] = 0 });
        Assert.Equal("started", run.GetProperty("state").GetString());
        string id = run.GetProperty("runId").GetString();
        var args = new Dictionary<string, object> { ["run_id"] = id, ["client_index"] = 1 };
        var launch = await CallAndValidate(client, "start_client", args);
        Assert.Equal(outcome, launch.GetProperty("outcome").GetString());
        if (code == null) Assert.Equal(JsonValueKind.Null, launch.GetProperty("error").ValueKind);
        else Assert.Equal(code, launch.GetProperty("error").GetProperty("code").GetString());
        if (outcome == "launched" || outcome == "launch_failed")
        {
            var repeated = await CallAndValidate(client, "start_client", args);
            Assert.Equal(outcome == "launched" ? "existing_running" : outcome, repeated.GetProperty("outcome").GetString());
            Assert.Equal(JsonValueKind.Null, repeated.GetProperty("preflight").ValueKind);
        }
        await CallAndValidate(client, "stop_run", new() { ["run_id"] = id });
        var rejected = await CallAndValidate(client, "start_client", new() { ["run_id"] = id, ["client_index"] = 2 });
        Assert.Equal("rejected", rejected.GetProperty("outcome").GetString());
        Assert.Equal("run_not_active", rejected.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, rejected.GetProperty("preflight").ValueKind);
    }

    private Task<McpClient> Connect(string scenario) => McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Harmless launch output-schema fixture",
        Command = Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe"),
        Arguments = new[] { directory, "launch-schema", scenario }, ShutdownTimeout = TimeSpan.FromSeconds(10),
    }), cancellationToken: timeout.Token);

    private async Task<JsonElement> CallAndValidate(McpClient client, string name, Dictionary<string, object> arguments)
    {
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        var tool = tools.Single(t => t.Name == name);
        var result = await client.CallToolAsync(name, arguments, cancellationToken: timeout.Token);
        Assert.NotEqual(true, result.IsError);
        Assert.True(result.StructuredContent.HasValue);
        Assert.True(tool.ReturnJsonSchema.HasValue);
        var outputSchema = tool.ReturnJsonSchema.Value;
        var schema = JsonSchema.FromText(outputSchema.GetRawText());
        var content = result.StructuredContent.Value;
        var evaluation = schema.Evaluate(content, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(evaluation.IsValid, name + ": " + JsonSerializer.Serialize(evaluation));
        // Prove validation exercises the advertised required fields rather than a permissive schema.
        var missingRequired = JsonNode.Parse(content.GetRawText()).AsObject();
        string required = outputSchema.GetProperty("required")[0].GetString();
        Assert.True(missingRequired.Remove(required));
        Assert.False(schema.Evaluate(missingRequired).IsValid);
        return content;
    }

    public void Dispose()
    {
        timeout.Dispose();
        Directory.Delete(directory, true);
    }
}
