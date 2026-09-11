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
            Assert.Equal(19, tools.Count);
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
}
