using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace CoopMcpServer.Tests;

public sealed class McpSetupTests
{
    [Fact]
    public async Task SetupPublishesOnlyStandaloneProjectAndPreservesLocalSettings()
    {
        using var fixture = new SetupFixture();
        string game = fixture.CreateGame();
        string dotnet = fixture.CreatePublisher();
        var first = await fixture.PowerShell("tools/mcp/setup.ps1", "-GameDirectory", game, "-DotnetPath", dotnet);
        Assert.Equal(0, first.Code);
        string profile = Path.Combine(fixture.Root, ".mcp-local", "profiles.json");
        using var json = JsonDocument.Parse(File.ReadAllText(profile));
        Assert.Equal(Path.Combine(game, "bin", "Win64_Shipping_Client", "Bannerlord.exe"),
            json.RootElement.GetProperty("profiles").GetProperty("local").GetProperty("executable").GetString());
        Assert.Equal(Path.Combine(fixture.Root, ".mcp-local", "runs"), json.RootElement.GetProperty("artifactDirectory").GetString());
        const string edited = "{\"artifactDirectory\":\"D:\\\\operator runs\",\"profiles\":{},\"operatorNote\":\"preserve bytes\"}\n";
        File.WriteAllText(profile, edited);
        var second = await fixture.PowerShell("tools/mcp/setup.ps1", "-DotnetPath", dotnet);
        Assert.Equal(0, second.Code);
        Assert.Equal(edited, File.ReadAllText(profile));
        string[] publishes = File.ReadAllLines(Path.Combine(fixture.Root, "publish calls.txt"));
        Assert.Equal(2, publishes.Length);
        Assert.All(publishes, line => Assert.Equal(Path.Combine(fixture.Root, "tools", "CoopMcpServer", "CoopMcpServer.csproj"), line));
        Assert.False(Directory.Exists(Path.Combine(game, "Modules")));
    }

    [Fact]
    public async Task SetupSupportsIsolatedRuntimeDirectory()
    {
        using var fixture = new SetupFixture();
        string runtime = Path.Combine(fixture.Root, "custom runtime output");
        var result = await fixture.PowerShell("tools/mcp/setup.ps1", "-GameDirectory", fixture.CreateGame(),
            "-RuntimeDirectory", runtime, "-DotnetPath", fixture.CreatePublisher());
        Assert.Equal(0, result.Code);
        Assert.True(File.Exists(Path.Combine(runtime, "profiles.json")));
        Assert.True(File.Exists(Path.Combine(runtime, "server", "CoopMcpServer.exe")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".mcp-local")));
    }

    [Fact]
    public async Task SetupScriptsParseWithoutExecutingInteractiveSetup()
    {
        using var fixture = new SetupFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "parse.ps1"),
            "$ErrorActionPreference = 'Stop'; Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -Filter *.ps1 | ForEach-Object { " +
            "$tokens = $null; $errors = $null; $null = [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors); " +
            "if ($errors.Count) { throw ($errors | Out-String) } }; exit 0");
        var result = await fixture.PowerShell("parse.ps1");
        Assert.Equal(0, result.Code);
        string prepare = File.ReadAllText(Path.Combine(fixture.Root, "prepare-links.ps1"));
        Assert.Contains("$ErrorActionPreference = 'Stop'", prepare);
        Assert.Contains("Join-Path $PSScriptRoot 'mb2'", prepare);
        Assert.Contains("if (!$setupSucceeded) { exit 1 }", prepare);
    }

    [Fact]
    public async Task SetupUsesRealJunctionWithoutTouchingTheGameDirectory()
    {
        using var fixture = new SetupFixture();
        string game = fixture.CreateGame();
        var link = await fixture.Run("cmd.exe", $"/d /s /c \"mklink /J \"{Path.Combine(fixture.Root, "mb2")}\" \"{game}\"\"");
        Assert.Equal(0, link.Code);
        try
        {
            var result = await fixture.PowerShell("tools/mcp/setup.ps1", "-DotnetPath", fixture.CreatePublisher());
            Assert.Equal(0, result.Code);
            Assert.Single(Directory.GetFiles(game, "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(Path.Combine(fixture.Root, "mb2")); }
    }

    [Fact]
    public async Task SetupFailureDoesNotCreateProfileOrContinuePublishing()
    {
        using var fixture = new SetupFixture();
        var noJunction = await fixture.PowerShell("tools/mcp/setup.ps1", "-DotnetPath", fixture.CreatePublisher());
        Assert.NotEqual(0, noJunction.Code);
        Assert.Contains("mb2 junction missing", noJunction.Error);
        var missing = await fixture.PowerShell("tools/mcp/setup.ps1", "-GameDirectory", fixture.Root, "-DotnetPath", fixture.CreatePublisher());
        Assert.NotEqual(0, missing.Code);
        Assert.Contains("Bannerlord.exe not found", missing.Error);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "publish calls.txt")));
        var failed = await fixture.PowerShell("tools/mcp/setup.ps1", "-GameDirectory", fixture.CreateGame(), "-DotnetPath", fixture.CreatePublisher(23));
        Assert.NotEqual(0, failed.Code);
        Assert.Contains("exit 23", failed.Error);
        Assert.False(File.Exists(Path.Combine(fixture.Root, ".mcp-local", "profiles.json")));
    }

    [Fact]
    public async Task MissingSetupFailsOnStderrWithoutBuildingOrSearchingAncestors()
    {
        using var fixture = new SetupFixture();
        var result = await fixture.PowerShell("tools/mcp/launch.ps1");
        Assert.NotEqual(0, result.Code);
        Assert.Equal("", result.Output);
        Assert.Contains("runmefirst.cmd", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".mcp-local")));
    }

    [Theory]
    [InlineData("pi")]
    [InlineData("codex")]
    public async Task AgentEntryAnchorsRootAndForwardsQuotedArgumentsAndExitCode(string client)
    {
        using var fixture = new SetupFixture();
        string stubs = Path.Combine(fixture.Root, "stub commands");
        Directory.CreateDirectory(stubs);
        File.WriteAllText(Path.Combine(stubs, client + ".cmd"), "@echo off\r\necho %CD%\r\necho [%~1]\r\necho [%~2]\r\nexit /b 37\r\n");
        string wrapper = Path.Combine(fixture.Root, "tools", "mcp", "start-" + client + ".cmd");
        var result = await fixture.Run("cmd.exe", $"/d /s /c \"\"{wrapper}\" \"argument with spaces\" --version\"", stubs);
        Assert.Equal(37, result.Code);
        Assert.Equal(new[] { fixture.Root, "[argument with spaces]", "[--version]" },
            result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(0, 11)]
    public async Task RunmefirstChainsOnlyAfterGamePathSuccessAndPropagatesFailures(int prepareExit, int setupExit)
    {
        using var fixture = new SetupFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "prepare-links.ps1"), $"[Console]::WriteLine('prepare stub'); exit {prepareExit}");
        File.WriteAllText(Path.Combine(fixture.Root, "tools", "mcp", "setup.ps1"), $"[Console]::WriteLine('setup stub'); exit {setupExit}");
        var result = await fixture.Run("cmd.exe", $"/d /s /c \"\"{Path.Combine(fixture.Root, "runmefirst.cmd")}\"\"");
        Assert.Equal(prepareExit != 0 ? prepareExit : setupExit, result.Code);
        Assert.Contains("prepare stub", result.Output);
        Assert.Equal(prepareExit == 0, result.Output.Contains("setup stub"));
    }

    [Fact]
    public async Task SharedLauncherInitializesOfficialSdkAndListsWorkflowToolsWithoutGame()
    {
        using var fixture = new SetupFixture();
        fixture.CopyServer();
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Root, ".mcp.json")));
        var definition = config.RootElement.GetProperty("mcpServers").GetProperty("bannerlord-coop");
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "repo setup smoke",
            Command = definition.GetProperty("command").GetString(),
            Arguments = definition.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()).ToArray(),
            WorkingDirectory = fixture.Root,
            ShutdownTimeout = TimeSpan.FromSeconds(10),
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        string[] expected = { "list_saves", "start_run", "start_client", "preflight_run", "capture_screenshot", "get_run", "wait_for_state", "list_commands", "execute_command",
            "join_client", "read_logs", "screenshot", "screenshot_status", "options_menu", "ui_inspect", "ui_action", "stop_run" };
        Assert.Equal(expected.Order(), tools.Select(tool => tool.Name).Order());
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".mcp-local", "runs")));
    }

    [Fact]
    public async Task SharedLauncherEofProducesNoStdoutNoiseAndExitsCleanly()
    {
        using var fixture = new SetupFixture();
        fixture.CopyServer();
        var result = await fixture.PowerShell("tools/mcp/launch.ps1");
        Assert.Equal(0, result.Code);
        Assert.Equal("", result.Output);
    }

    private sealed class SetupFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Coop MCP setup " + Guid.NewGuid().ToString("N"));
        private readonly string outside;

        public SetupFixture()
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "runmefirst.cmd"))) repo = repo.Parent;
            Assert.NotNull(repo);
            Directory.CreateDirectory(Root);
            outside = Path.Combine(Root, "unrelated cwd", "nested");
            Directory.CreateDirectory(outside);
            foreach (string file in new[] { "runmefirst.cmd", "prepare-links.ps1", ".mcp.json", "tools/mcp/setup.ps1", "tools/mcp/launch.ps1", "tools/mcp/start-pi.cmd", "tools/mcp/start-codex.cmd" })
            {
                string target = Path.Combine(Root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(repo.FullName, file), target);
            }
        }

        public string CreateGame()
        {
            string game = Path.Combine(Root, "fake game install");
            string bin = Path.Combine(game, "bin", "Win64_Shipping_Client");
            Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(bin, "Bannerlord.exe"), "not executable, never launched");
            return game;
        }

        public string CreatePublisher(int exitCode = 0)
        {
            string path = Path.Combine(Root, "fake dotnet.cmd");
            File.WriteAllText(path, "@echo off\r\n" +
                $"if not {exitCode}==0 exit /b {exitCode}\r\n" +
                $"echo %~2>>\"{Path.Combine(Root, "publish calls.txt")}\"\r\n" +
                "if not exist \"%~8\" mkdir \"%~8\"\r\necho stub>\"%~8\\CoopMcpServer.exe\"\r\nexit /b 0\r\n");
            return path;
        }

        public void CopyServer()
        {
            string runtime = Path.Combine(Root, ".mcp-local");
            string server = Path.Combine(runtime, "server");
            Directory.CreateDirectory(server);
            foreach (string file in Directory.GetFiles(AppContext.BaseDirectory)) File.Copy(file, Path.Combine(server, Path.GetFileName(file)));
            File.WriteAllText(Path.Combine(runtime, "profiles.json"), JsonSerializer.Serialize(new
            {
                artifactDirectory = Path.Combine(runtime, "runs"), profiles = new { },
            }));
        }

        public Task<ProcessResult> PowerShell(string script, params string[] arguments)
        {
            var info = new ProcessStartInfo("powershell.exe");
            foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Root, script) }.Concat(arguments)) info.ArgumentList.Add(arg);
            return Run(info);
        }

        public Task<ProcessResult> Run(string command, string arguments, string extraPath = null)
        {
            var info = new ProcessStartInfo(command, arguments);
            if (extraPath != null) info.Environment["PATH"] = extraPath + ";" + Environment.GetEnvironmentVariable("PATH");
            return Run(info);
        }

        private async Task<ProcessResult> Run(ProcessStartInfo info)
        {
            info.UseShellExecute = false;
            info.WorkingDirectory = outside;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using var process = Process.Start(info);
            process.StandardInput.Close();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(timeout.Token);
                return new ProcessResult(process.ExitCode, await output, await error);
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }

        public void Dispose() { Directory.Delete(Root, true); }
    }

    private sealed record ProcessResult(int Code, string Output, string Error);
}
