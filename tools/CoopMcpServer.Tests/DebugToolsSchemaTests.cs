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

    [Theory]
    [InlineData("deployed")]
    [InlineData("rolled_back")]
    [InlineData("rollback_failed")]
    [InlineData("failed_before_apply")]
    public async Task DeploymentOutputMatchesSchemaIncludingOriginallyAbsentTargets(string state)
    {
        await using var client = await Connect("ready");
        var report = await CallAndValidate(client, "deploy_mod", new() { ["solution_path"] = "fixture source/Coop.sln", ["profile"] = state });
        Assert.Equal(state, report.GetProperty("state").GetString());
        Assert.Equal("Release", report.GetProperty("configuration").GetString());
        if (state != "failed_before_apply") Assert.Equal(JsonValueKind.Null, report.GetProperty("files")[0].GetProperty("original").ValueKind);
    }

    [Theory]
    [InlineData("Release")]
    [InlineData("Debug")]
    public async Task DeploymentConfigurationMatchesInputAndOutputSchema(string configuration)
    {
        await using var client = await Connect("ready");
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        var schema = tools.Single(t => t.Name == "deploy_mod").JsonSchema;
        Assert.Equal("Release", schema.GetProperty("properties").GetProperty("configuration").GetProperty("default").GetString());
        Assert.DoesNotContain(schema.GetProperty("required").EnumerateArray(), p => p.GetString() == "configuration");
        var report = await CallAndValidate(client, "deploy_mod", new() { ["solution_path"] = "fixture source/Coop.sln", ["profile"] = "deployed", ["configuration"] = configuration });
        Assert.Equal(configuration, report.GetProperty("configuration").GetString());
    }

    [Fact]
    public async Task DriftWaitSdkWireCarriesOnlyFinalCamelCaseResponseAtScriptPath()
    {
        const string operation = "ea451533-21af-47ce-b889-887ee8da5e71", incarnation = "72a57da1-88b5-4f7e-9111-71f5c7a965f4";
        await using var client = await Connect("drift");
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(23, tools.Count);
        var tool = tools.Single(t => t.Name == "wait_for_drift");
        var schema = JsonSchema.FromText(tool.ReturnJsonSchema.Value.GetRawText());
        string id = (await CallAndValidate(client, "start_run", new() { ["profile"] = "fixture", ["client_count"] = 2 })).GetProperty("runId").GetString();
        var wire = new JsonObject();
        string export = Environment.GetEnvironmentVariable("COOP_MCP_DRIFT_WIRE_EXPORT");
        foreach (string instance in new[] { "client1", "client2" })
        {
            var result = await client.CallToolAsync("wait_for_drift", new Dictionary<string, object>
            {
                ["run_id"] = id, ["instance"] = instance, ["operation_id"] = operation, ["incarnation"] = incarnation, ["timeout_seconds"] = 40,
            }, cancellationToken: timeout.Token);
            wire[instance] = JsonSerializer.SerializeToNode(result, ModelContextProtocol.McpJsonUtilities.DefaultOptions);
            if (export != null) File.WriteAllText(export, wire.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Assert.NotEqual(true, result.IsError);
            Assert.True(schema.Evaluate(result.StructuredContent.Value).IsValid);
            // Same path the timed-protocol script reads: structuredContent.result.response.result.structuredResult.
            var wait = result.StructuredContent.Value.GetProperty("result");
            Assert.Equal("ended", wait.GetProperty("outcome").GetString());
            Assert.Equal("duration_complete", wait.GetProperty("ended").GetString());
            Assert.Equal(2, wait.GetProperty("polls").GetInt32());
            // The SDK omits null members on the wire, so a final success carries no lastError.
            Assert.False(wait.TryGetProperty("lastError", out var lastError) && lastError.ValueKind != JsonValueKind.Null);
            var response = wait.GetProperty("response");
            Assert.False(response.TryGetProperty("Result", out _));
            Assert.True(response.GetProperty("ok").GetBoolean());
            Assert.Equal(wait.GetProperty("responseId").GetString(), response.GetProperty("id").GetString());
            var command = response.GetProperty("result");
            Assert.True(command.GetProperty("succeeded").GetBoolean());
            Assert.True(command.GetProperty("hasStructuredResult").GetBoolean());
            var drift = command.GetProperty("structuredResult");
            Assert.Equal(operation, drift.GetProperty("operationId").GetString());
            Assert.Equal(incarnation, drift.GetProperty("incarnation").GetString());
            Assert.Equal("duration_complete", drift.GetProperty("ended").GetString());
            Assert.Equal(30.0, drift.GetProperty("sampledSpanSeconds").GetDouble());
            Assert.True(drift.GetProperty("completeCoverage").GetBoolean());
            Assert.All(drift.GetProperty("rows").EnumerateArray(), row =>
            {
                Assert.Equal(0, row.GetProperty("invalid").GetInt32());
                Assert.Equal(0, row.GetProperty("occupancyLoss").GetInt32());
            });
            Assert.Equal(10, drift.GetProperty("rows").GetArrayLength());
        }
        await CallAndValidate(client, "stop_run", new() { ["run_id"] = id });
    }

    [Fact]
    public async Task ControlWaitSdkWireCarriesOnlyFinalCamelCaseResponseAtScriptPath()
    {
        const string incarnation = "72a57da1-88b5-4f7e-9111-71f5c7a965f4";
        string[][] operations = { new[] { "6d58832a-f320-4d32-aed7-29a7b4679622", "e65f0312-c6a5-4d46-9b80-98bae39c72c2" },
            new[] { "b98d8246-831c-446d-b686-8cc1874cea62", "158bbdfc-8c80-4587-a15c-8058e0b189cf" } };
        await using var client = await Connect("control");
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(23, tools.Count);
        var schema = JsonSchema.FromText(tools.Single(t => t.Name == "wait_for_control").ReturnJsonSchema.Value.GetRawText());
        string id = (await CallAndValidate(client, "start_run", new() { ["profile"] = "fixture", ["client_count"] = 2 })).GetProperty("runId").GetString();
        var wire = new JsonObject();
        string export = Environment.GetEnvironmentVariable("COOP_MCP_CONTROL_WIRE_EXPORT");
        for (int slot = 0; slot < 2; slot++)
            foreach (bool neutral in new[] { false, true })
            {
                string instance = "client" + (slot + 1), operation = operations[slot][neutral ? 1 : 0];
                var result = await client.CallToolAsync("wait_for_control", new Dictionary<string, object>
                {
                    ["run_id"] = id, ["instance"] = instance, ["operation_id"] = operation, ["incarnation"] = incarnation, ["slot"] = slot,
                    ["require_neutral"] = neutral, ["timeout_seconds"] = 5,
                }, cancellationToken: timeout.Token);
                wire[instance + (neutral ? ":neutral" : ":pulse")] = JsonSerializer.SerializeToNode(result, ModelContextProtocol.McpJsonUtilities.DefaultOptions);
                if (export != null) File.WriteAllText(export, wire.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                Assert.NotEqual(true, result.IsError);
                Assert.True(schema.Evaluate(result.StructuredContent.Value).IsValid);
                // Same path the timed-protocol script reads: structuredContent.result.response.result.structuredResult.status.
                var wait = result.StructuredContent.Value.GetProperty("result");
                Assert.Equal("completed", wait.GetProperty("outcome").GetString());
                Assert.True(wait.GetProperty("reached").GetBoolean());
                Assert.Equal("completed_axes_neutral_requested", wait.GetProperty("phase").GetString());
                // The fixture shows the previous state first, so a prior or empty operation was polled and not accepted.
                Assert.Equal(3, wait.GetProperty("polls").GetInt32());
                var response = wait.GetProperty("response");
                Assert.False(response.TryGetProperty("Result", out _));
                Assert.True(response.GetProperty("ok").GetBoolean());
                Assert.Equal(wait.GetProperty("responseId").GetString(), response.GetProperty("id").GetString());
                var command = response.GetProperty("result");
                Assert.True(command.GetProperty("succeeded").GetBoolean());
                var control = command.GetProperty("structuredResult");
                Assert.Equal(incarnation, control.GetProperty("incarnation").GetString());
                Assert.Equal(1, control.GetProperty("host").GetProperty("Epoch").GetInt32());
                var status = control.GetProperty("status");
                Assert.Equal(incarnation, status.GetProperty("IncarnationId").GetString());
                Assert.Equal(slot, status.GetProperty("ship").GetInt32());
                Assert.Equal(operation, status.GetProperty("operationId").GetString());
                Assert.True(status.GetProperty("pulseNeutralInputSequence").GetInt64() > status.GetProperty("pulseLastInputSequence").GetInt64());
                var application = status.GetProperty("currentOwnerApplication")[slot];
                if (neutral)
                {
                    Assert.Equal(0, application.GetProperty("rudder").GetDouble());
                    Assert.Equal(0, application.GetProperty("longitudinal").GetInt32());
                }
                else Assert.Equal(slot == 0 ? 0.7 : -0.7, application.GetProperty("rudder").GetDouble(), 3);
            }
        await CallAndValidate(client, "stop_run", new() { ["run_id"] = id });
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
