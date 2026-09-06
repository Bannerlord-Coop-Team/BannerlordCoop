using ModelContextProtocol.Client;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed class McpStdioTests
{
    [Fact]
    public async Task StdioEofStopsTheMcpHostWithoutForceKillingIt()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CoopMcpEof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string config = Path.Combine(directory, "profiles.json");
            File.WriteAllText(config, JsonSerializer.Serialize(new { artifactDirectory = directory, profiles = new { } }));
            var info = new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.exe"))
            {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            info.ArgumentList.Add("--config");
            info.ArgumentList.Add(config);
            using var process = System.Diagnostics.Process.Start(info);
            Task<string> errors = process.StandardError.ReadToEndAsync();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            try
            {
                process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token);
                Assert.True(process.ExitCode == 0, await errors);
                Assert.Equal("", await output);
            }
            finally { if (!process.HasExited) process.Kill(); }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task InitializeAndToolsListWorkWithoutGameOrConfiguredProfiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CoopMcpSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string config = Path.Combine(directory, "profiles.json");
            File.WriteAllText(config, JsonSerializer.Serialize(new { artifactDirectory = directory, profiles = new { } }));
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "Coop MCP smoke",
                Command = Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.exe"),
                Arguments = new[] { "--config", config },
                ShutdownTimeout = TimeSpan.FromSeconds(10),
            });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            string[] expected = { "start_run", "get_run", "wait_for_state", "list_commands", "execute_command",
                "join_client", "read_logs", "screenshot", "screenshot_status", "stop_run" };
            Assert.Equal(expected.Order(), tools.Select(t => t.Name).Order());
            var result = await client.CallToolAsync("get_run", new Dictionary<string, object> { ["run_id"] = "missing" }, cancellationToken: timeout.Token);
            Assert.True(result.IsError);
        }
        finally { Directory.Delete(directory, true); }
    }
}
