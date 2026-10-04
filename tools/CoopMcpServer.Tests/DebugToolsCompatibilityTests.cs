using ModelContextProtocol.Client;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CoopMcpServer.Tests;

public sealed class DebugToolsCompatibilityTests
{
    [Fact]
    public async Task OfficialSdkPreservesSeventeenToolDefinitionsAndAddsDeploymentAndBoundedLayers()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CoopLayerSchema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string config = Path.Combine(directory, "profiles.json");
            File.WriteAllText(config, JsonSerializer.Serialize(new { artifactDirectory = directory, profiles = new { } }));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            string executable = Environment.GetEnvironmentVariable("COOP_MCP_SCHEMA_EXECUTABLE") ??
                Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.exe");
            await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "Read-only standalone schema compatibility", Command = executable,
                Arguments = new[] { "--config", config }, ShutdownTimeout = TimeSpan.FromSeconds(10),
            }), cancellationToken: timeout.Token);
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            Assert.Equal(23, tools.Count);
            var baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "DebugTools-5192afea.json"))).AsArray();
            Assert.Equal(17, baseline.Count);
            foreach (var original in baseline)
            {
                string name = original["name"].GetValue<string>();
                var actual = JsonSerializer.SerializeToNode(tools.Single(t => t.Name == name).ProtocolTool);
                if (name == "ui_inspect" || name == "ui_action")
                {
                    Assert.False(string.IsNullOrWhiteSpace(actual["description"].GetValue<string>()));
                    actual["description"] = original["description"].DeepClone();
                }
                if (name == "start_run")
                {
                    Assert.Contains("client_count=2 starts server, client1 and client2", actual["description"].GetValue<string>());
                    Assert.Contains("DebugAutoConnect (/autoconnect), not join_client", actual["description"].GetValue<string>());
                    Assert.Contains("client_count=0 then start_client, readyToJoin and join_client", actual["description"].GetValue<string>());
                    actual["description"] = original["description"].DeepClone();
                }
                if (name == "start_client")
                {
                    Assert.Contains("For manual joining, use start_run with client_count=0", actual["description"].GetValue<string>());
                    Assert.Contains("Launching is not joining; wait readyToJoin, join_client", actual["description"].GetValue<string>());
                    actual["description"] = original["description"].DeepClone();
                }
                if (name == "wait_for_state")
                {
                    Assert.Contains("Client readyForCampaignTests waits also try closing", actual["description"].GetValue<string>());
                    Assert.Contains("never replayed after uncertainty", actual["description"].GetValue<string>());
                    Assert.NotEqual(true, tools.Single(t => t.Name == name).ProtocolTool.Annotations?.ReadOnlyHint);
                    Assert.Null(actual["annotations"]);
                    actual["description"] = original["description"].DeepClone();
                    actual["annotations"] = original["annotations"].DeepClone();
                }
                if (name is "start_run" or "get_run" or "start_client" or "stop_run")
                {
                    var runSchema = name == "start_client" ? actual["outputSchema"]["properties"]["run"] : actual["outputSchema"];
                    var instance = runSchema["properties"]["instances"]["items"];
                    var response = baseline.Single(t => t["name"].GetValue<string>() == "ui_action")["outputSchema"];
                    AssertStartupPopupSchema(instance["properties"]["startupPopup"], response);
                    Assert.DoesNotContain(instance["required"].AsArray(), field => field.GetValue<string>() == "startupPopup");
                    Assert.True(instance["properties"].AsObject().Remove("startupPopup"));
                }
                if (name == "ui_inspect")
                {
                    var properties = actual["inputSchema"]["properties"].AsObject();
                    Assert.Equal("string", properties["layer"]["type"].GetValue<string>());
                    Assert.DoesNotContain(actual["inputSchema"]["required"].AsArray(), field => field.GetValue<string>() == "layer");
                    Assert.True(properties.Remove("layer"));
                }
                Assert.True(JsonNode.DeepEquals(original, actual), "Unexpected schema or metadata change: " + name);
            }
            var deployment = tools.Single(t => t.Name == "deploy_mod");
            Assert.Equal(new[] { "configuration", "profile", "solution_path" }, deployment.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(new[] { "profile", "solution_path" }, deployment.JsonSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
            Assert.Equal("Release", deployment.JsonSchema.GetProperty("properties").GetProperty("configuration").GetProperty("default").GetString());
            Assert.True(deployment.ReturnJsonSchema.HasValue);
            var layers = tools.Single(t => t.Name == "ui_layers");
            Assert.Equal(new[] { "instance", "run_id" }, layers.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(new[] { "instance", "run_id" }, layers.JsonSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
            Assert.True(layers.ProtocolTool.Annotations.ReadOnlyHint);
            Assert.True(layers.ReturnJsonSchema.HasValue);
            var drift = tools.Single(t => t.Name == "wait_for_drift");
            string[] driftArguments = { "incarnation", "instance", "operation_id", "run_id", "timeout_seconds" };
            Assert.Equal(driftArguments, drift.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(driftArguments, drift.JsonSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
            Assert.True(drift.ProtocolTool.Annotations.ReadOnlyHint);
            Assert.True(drift.ReturnJsonSchema.HasValue);
            var control = tools.Single(t => t.Name == "wait_for_control");
            string[] controlArguments = { "incarnation", "instance", "operation_id", "require_neutral", "run_id", "slot", "timeout_seconds" };
            Assert.Equal(controlArguments, control.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(controlArguments, control.JsonSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
            Assert.True(control.ProtocolTool.Annotations.ReadOnlyHint);
            Assert.True(control.ReturnJsonSchema.HasValue);
            var lab = tools.Single(t => t.Name == "wait_for_lab");
            string[] labArguments = { "incarnation", "run_id", "stage", "timeout_seconds" };
            Assert.Equal(labArguments, lab.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(labArguments, lab.JsonSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
            Assert.True(lab.ProtocolTool.Annotations.ReadOnlyHint);
            Assert.True(lab.ReturnJsonSchema.HasValue);
            var video = tools.Single(t => t.Name == "record_video");
            string[] videoArguments = { "instance", "run_id", "seconds" };
            Assert.Equal(videoArguments, video.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(videoArguments, video.JsonSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order());
            Assert.True(video.ProtocolTool.Annotations.ReadOnlyHint);
            string export = Environment.GetEnvironmentVariable("COOP_MCP_SCHEMA_EXPORT");
            if (export != null)
                File.WriteAllText(export, JsonSerializer.Serialize(tools.OrderBy(t => t.Name).Select(t => t.ProtocolTool),
                    new JsonSerializerOptions { WriteIndented = true }));
            // No configured profile can launch a game; discovery alone must not create a run.
            Assert.Equal(new[] { config }, Directory.GetFiles(directory));
            Assert.Empty(Directory.GetDirectories(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void AssertStartupPopupSchema(JsonNode actual, JsonNode response)
    {
        var action = response.DeepClone();
        action["type"] = new JsonArray("object", "null");
        action["default"] = null;
        var error = response["properties"]["error"].DeepClone();
        error["default"] = null;
        var expected = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["outcome"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
                ["action"] = action,
                ["error"] = error,
            },
            ["required"] = new JsonArray("outcome"),
            ["default"] = null,
        };
        Assert.True(JsonNode.DeepEquals(expected, actual), "Unexpected startupPopup schema.");
    }
}
