using ModelContextProtocol;
using System.Text.Json;

namespace CoopMcpServer.Tests;

// External-server runs own clients only, and owned runs keep their server checks.
public sealed partial class RunOrchestratorTests
{
    // Builds the tool surface over this fixture's orchestrator.
    private DebugTools Tools() => new(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder()));

    [Fact]
    public async Task ExternalRunLaunchesAndBudgetsOnlyClients()
    {
        var run = await runs.StartAsync("external", 2, default);
        Assert.Equal(new[] { "client", "client" }, launcher.Roles);
        Assert.Equal(new[] { "client1", "client2" }, run.Instances.Select(i => i.Name));
        Assert.Equal(new[] { "testclient1", "testclient2" }, run.Instances.Select(i => i.Identity.PlatformId));
        Assert.Equal(new[] { 2 }, preflight.Counts);
    }

    [Fact]
    public void PreflightToolCountsAServerOnlyWhenTheRunOwnsOne()
    {
        runs.Preflight("external", 2);
        runs.Preflight("test", 2);
        Assert.Equal(new[] { 2, 3 }, preflight.Counts);
    }

    [Fact]
    public async Task ExternalRunStartsEmptyThenAddsAClient()
    {
        var run = await runs.StartAsync("external", 0, default);
        Assert.Equal("started", run.State);
        Assert.Empty(run.Instances);
        Assert.Empty(launcher.Processes);
        var added = await runs.StartClientAsync(run.RunId, 1, default);
        Assert.Equal("launched", added.Outcome);
        Assert.Equal("client1", Assert.Single(added.Run.Instances).Name);
        Assert.Equal("client", Assert.Single(launcher.Roles));
    }

    [Fact]
    public async Task StoppedExternalRunRejectsNewClients()
    {
        var run = await runs.StartAsync("external", 0, default);
        await runs.StopAsync(run.RunId);
        var added = await runs.StartClientAsync(run.RunId, 1, default);
        Assert.Equal("rejected", added.Outcome);
        Assert.Equal("run_not_active", added.Error.Code);
        Assert.Empty(launcher.Processes);
    }

    [Fact]
    public async Task ExternalRunRejectsSaveNameBeforeLaunching()
    {
        File.WriteAllText(Path.Combine(directory, "Danustica campaign.sav"), "opaque save bytes");
        var error = await Assert.ThrowsAsync<McpException>(() => runs.StartAsync("external", 1, default, "Danustica campaign"));
        Assert.Contains("external dedicated server", error.Message);
        Assert.Empty(launcher.Processes);
        Assert.Empty(preflight.Counts);
    }

    [Fact]
    public async Task ExternalRunRejectsServerInstanceWithoutTouchingAnyBridge()
    {
        var run = await runs.StartAsync("external", 1, default);
        var tools = Tools();
        Func<Task>[] calls =
        {
            () => tools.WaitForState(run.RunId, "server", "readyForCampaignTests", 1, default),
            () => tools.WaitForState(run.RunId, "server", "exited", 1, default),
            () => tools.ExecuteCommand(run.RunId, "server", "coop.debug.town.list_towns", Array.Empty<string>(), default),
            () => tools.ListCommands(run.RunId, "server", default),
            () => tools.JoinClient(run.RunId, "server", default),
            () => tools.ReadLogs(run.RunId, "server", default),
            () => tools.Screenshot(run.RunId, "server", default),
            () => tools.CaptureScreenshot(run.RunId, "server", 1, default),
        };
        foreach (var call in calls)
            Assert.Contains("external", (await Assert.ThrowsAsync<McpException>(call)).Message);
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task ExternalJoinChecksOnlyTheClientBridgeThenSends()
    {
        var run = await runs.StartAsync("external", 1, default);
        pipe.Status = new { assemblyMvid = Guid.Empty, protocolVersion = 1 };
        var response = await Tools().JoinClient(run.RunId, "client1", default);
        Assert.True(response.Ok);
        Assert.Equal(new[] { "status", "join" }, pipe.Methods);
        Assert.Equal(new[] { "client", "client" }, pipe.Roles);
    }

    [Fact]
    public async Task ExternalJoinStillRefusesAMismatchedClientBuild()
    {
        var run = await runs.StartAsync("external", 1, default);
        pipe.Status = new { assemblyMvid = Guid.NewGuid(), protocolVersion = 1 };
        Assert.Equal("bridge_build_mismatch", (await Tools().JoinClient(run.RunId, "client1", default)).Error.Code);
        Assert.DoesNotContain("join", pipe.Methods);
        preflight.Mvid = Guid.NewGuid();
        Assert.Equal("bridge_build_changed", (await Tools().JoinClient(run.RunId, "client1", default)).Error.Code);
        Assert.DoesNotContain("join", pipe.Methods);
    }

    [Theory]
    [InlineData("readyToJoin")]
    [InlineData("readyForCampaignTests")]
    public async Task ExternalWaitReadsOnlyTheClientBridge(string state)
    {
        var run = await runs.StartAsync("external", 1, default);
        pipe.Status = new { readyForClientJoin = true, readyForCampaignTests = true, topScreen = "SandBox.View.Map.MapScreen" };
        var result = JsonSerializer.SerializeToElement(await Tools().WaitForState(run.RunId, "client1", state, 5, default));
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.NotEmpty(pipe.Roles);
        Assert.All(pipe.Roles, role => Assert.Equal("client", role));
    }

    [Fact]
    public async Task ExternalRunKeepsClientCommandScreenshotAndLogTools()
    {
        string log = Path.Combine(directory, "external-client.log");
        File.WriteAllText(log, "client log");
        var run = await runs.StartAsync("external", 1, default);
        pipe.Status = new { logPath = log };
        var tools = Tools();
        await tools.ExecuteCommand(run.RunId, "client1", "coop.debug.town.list_towns", Array.Empty<string>(), default);
        await tools.Screenshot(run.RunId, "client1", default);
        Assert.Equal("client log", (await tools.ReadLogs(run.RunId, "client1", default)).Text);
        Assert.Equal(new[] { "command", "screenshot", "status" }, pipe.Methods);
        Assert.All(pipe.Roles, role => Assert.Equal("client", role));
    }

    [Fact]
    public async Task ExternalStopCleansOnlyItsClientsAndArchivesTheirLogs()
    {
        string log = Path.Combine(directory, "external-client.log");
        File.WriteAllText(log, "client log");
        pipe.Status = new { logPath = log };
        var run = await runs.StartAsync("external", 2, default);
        var stopped = await Tools().StopRun(run.RunId);
        Assert.Equal("stopped", stopped.State);
        Assert.Equal(2, launcher.Processes.Count);
        Assert.All(launcher.Processes, process => Assert.Equal(1, process.Stops));
        Assert.Equal(2, pipe.Methods.Count(m => m == "shutdown"));
        Assert.All(pipe.Roles, role => Assert.Equal("client", role));
        Assert.Equal("client log", File.ReadAllText(Path.Combine(run.ArtifactDirectory, "client1.log")));
        Assert.Equal("stopped", (await runs.StopAsync(run.RunId)).State);
        Assert.All(launcher.Processes, process => Assert.Equal(1, process.Stops));
    }

    [Fact]
    public async Task EmptyExternalRunStopsAndAllowsANewRun()
    {
        var run = await runs.StartAsync("external", 0, default);
        Assert.Equal("stopped", (await runs.StopAsync(run.RunId)).State);
        Assert.Empty(pipe.Methods);
        Assert.Equal("started", (await runs.StartAsync("external", 0, default)).State);
    }

    [Fact]
    public async Task ExternalPartialLaunchFailureStopsOnlyTheLaunchedClient()
    {
        launcher.FailAt = 2;
        pipe.Throw = true;
        var run = await runs.StartAsync("external", 2, default);
        Assert.Equal("launch_failed", run.State);
        Assert.Equal("client", Assert.Single(launcher.Roles));
        Assert.Equal(1, Assert.Single(launcher.Processes).Stops);
    }

    [Fact]
    public async Task OwnedRunJoinStillRequiresItsLiveServer()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Status = new { assemblyMvid = Guid.Empty, protocolVersion = 1 };
        await launcher.Processes[0].StopAsync(TimeSpan.Zero);
        var response = await runs.RequestAsync(run.RunId, "client1", "join", new { }, true, default);
        Assert.Equal("server_exited", response.Error.Code);
        Assert.DoesNotContain("join", pipe.Methods);
    }

    [Fact]
    public async Task OwnedRunJoinStillChecksItsServerBridge()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Status = new { assemblyMvid = Guid.Empty, protocolVersion = 1 };
        Assert.True((await runs.RequestAsync(run.RunId, "client1", "join", new { }, true, default)).Ok);
        Assert.Equal(new[] { "client", "server", "client" }, pipe.Roles);
    }

    [Fact]
    public async Task OwnedRunRejectsNewClientsAfterItsServerExits()
    {
        var run = await runs.StartAsync("test", 0, default);
        await launcher.Processes[0].StopAsync(TimeSpan.Zero);
        var added = await runs.StartClientAsync(run.RunId, 1, default);
        Assert.Equal("rejected", added.Outcome);
        Assert.Equal("run_not_active", added.Error.Code);
        Assert.Single(launcher.Processes);
    }

    [Fact]
    public void ProfileJsonEnablesExternalServerAndDefaultsToOwned()
    {
        string path = Path.Combine(directory, "profiles.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            artifactDirectory = directory,
            profiles = new { dedicated = new { externalServer = true }, local = new { } },
        }));
        var settings = CoopMcpServerSettings.Load(path);
        Assert.True(settings.Profiles["dedicated"].ExternalServer);
        Assert.False(settings.Profiles["local"].ExternalServer);
    }
}
