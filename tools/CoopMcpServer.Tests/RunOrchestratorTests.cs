using Common.LiveTesting;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed class RunOrchestratorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CoopMcpServerTests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeLauncher launcher = new();
    private readonly FakePipe pipe = new();
    private readonly RunOrchestrator runs;
    private readonly FakePreflight preflight = new();

    public RunOrchestratorTests()
    {
        Directory.CreateDirectory(directory);
        string executable = Path.Combine(directory, "Bannerlord.exe");
        File.WriteAllText(executable, "not executable; fake launcher only");
        var settings = new CoopMcpServerSettings
        {
            ArtifactDirectory = directory,
            Profiles = new() { ["test"] = new LaunchProfile { Executable = executable } },
        };
        runs = new RunOrchestrator(settings, launcher, pipe, new IncrementalLogReader(), preflight, new SaveCatalog(new FakeSaveDirectory(directory)));
    }

    [Theory]
    [InlineData("click")]
    [InlineData("toggle")]
    [InlineData("text")]
    [InlineData("slider")]
    [InlineData("scroll_vertical")]
    [InlineData("scroll_horizontal")]
    public async Task GenericUiActionsPreserveReferencesArgumentsAndMutationClassification(string action)
    {
        var run = await runs.StartAsync("test", 1, default);
        await new DebugTools(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder())).UiAction(run.RunId, "client1", "snapshot", "e7", action, default, "Danustica", 0.5);
        Assert.Equal("ui-action", Assert.Single(pipe.Methods));
        Assert.True(pipe.LastMutation);
        var args = JsonSerializer.SerializeToElement(pipe.LastParameters);
        Assert.Equal("snapshot", args.GetProperty("snapshot").GetString());
        Assert.Equal("e7", args.GetProperty("element").GetString());
        Assert.Equal(action, args.GetProperty("action").GetString());
        Assert.Equal("Danustica", args.GetProperty("text").GetString());
        Assert.Equal(0.5, args.GetProperty("value").GetDouble());
    }

    [Fact]
    public async Task GenericUiInspectionIsReadOnlyAndPreservesPagination()
    {
        var run = await runs.StartAsync("test", 1, default);
        await new DebugTools(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder())).UiInspect(run.RunId, "client1", default, "snapshot", 128);
        Assert.Equal("ui-inspect", Assert.Single(pipe.Methods));
        Assert.False(pipe.LastMutation);
        var args = JsonSerializer.SerializeToElement(pipe.LastParameters);
        Assert.Equal(128, args.GetProperty("offset").GetInt32());
    }

    [Theory]
    [InlineData("eval", null, null)]
    [InlineData("set_property", null, null)]
    [InlineData("click", null, double.NaN)]
    [InlineData("click", null, double.PositiveInfinity)]
    public async Task GenericUiRejectsUnboundedActionsBeforePipe(string action, string text, double? value)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new DebugTools(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder())).UiAction("missing", "client1", "snapshot", "e0", action, default, text, value));
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task GenericUiBoundsTextAndPages()
    {
        var tools = new DebugTools(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder()));
        await Assert.ThrowsAsync<ArgumentException>(() => tools.UiAction("missing", "client1", "snapshot", "e0", "text", default, new string('x', 513)));
        await Assert.ThrowsAsync<ArgumentException>(() => tools.UiInspect("missing", "client1", default, null, 128));
        await Assert.ThrowsAsync<ArgumentException>(() => tools.UiInspect("missing", "client1", default, "snapshot", 16385));
        Assert.Empty(pipe.Methods);
    }

    [Theory]
    [InlineData("open", true)]
    [InlineData("select", true)]
    [InlineData("inspect", false)]
    [InlineData("close", true)]
    public async Task OptionsMenuUsesBoundedProtocolAndPreservesMutationClassification(string action, bool mutation)
    {
        var run = await runs.StartAsync("test", 1, default);
        var tools = new DebugTools(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder()));
        await tools.OptionsMenu(run.RunId, "client1", action, default, "ChatTab");
        Assert.Equal("options-menu", Assert.Single(pipe.Methods));
        Assert.Equal(mutation, pipe.LastMutation);
        var parameters = JsonSerializer.SerializeToElement(pipe.LastParameters);
        Assert.Equal(action, parameters.GetProperty("action").GetString());
        Assert.Equal("ChatTab", parameters.GetProperty("tab").GetString());
    }

    [Theory]
    [InlineData("apply", "VoiceTab")]
    [InlineData("click", "VoiceTab")]
    [InlineData("select", "")]
    public async Task OptionsMenuRejectsUnboundedActionsBeforeRequest(string action, string tab)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new DebugTools(runs, new ScreenshotCapture(runs, new ScreenshotImageEncoder())).OptionsMenu("missing", "client1", action, default, tab));
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task StartLaunchesDistinctOwnedInstancesWithoutImplyingReadinessOrJoining()
    {
        var run = await runs.StartAsync("test", 2, default);
        Assert.Equal(3, run.Instances.Length);
        Assert.Equal(3, run.Instances.Select(i => i.Identity.PlatformId).Distinct().Count());
        Assert.All(run.Instances, i => { Assert.True(i.ProcessAlive); Assert.Null(i.Status); Assert.Equal(run.RunId, i.Identity.RunToken); });
        Assert.Empty(pipe.Methods);
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "run.json")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runs.StartAsync("test", 0, default));
    }

    [Fact]
    public async Task PartialLaunchFailureStopsOnlySuccessfullyOwnedProcesses()
    {
        launcher.FailAt = 2;
        pipe.Throw = true;
        var run = await runs.StartAsync("test", 2, default);
        Assert.Equal("launch_failed", run.State);
        Assert.Single(launcher.Processes);
        Assert.All(launcher.Processes, p => Assert.Equal(1, p.Stops));
        Assert.All(run.Instances, i => Assert.False(i.ProcessAlive));
    }

    [Fact]
    public async Task CommandsAreSerializedPerInstanceAndUncertainMutationIsNotRetried()
    {
        var run = await runs.StartAsync("test", 0, default);
        pipe.Delay = 30;
        pipe.Uncertain = true;
        var first = runs.RequestAsync(run.RunId, "server", "command", new { name = "coop.capture", arguments = new[] { "one two" } }, true, default);
        var second = runs.RequestAsync(run.RunId, "server", "command", new { name = "coop.capture", arguments = Array.Empty<string>() }, true, default);
        var responses = await Task.WhenAll(first, second);
        Assert.Equal(1, pipe.MaxConcurrent);
        Assert.Equal(2, pipe.Methods.Count);
        Assert.All(responses, r => Assert.True(r.Error.OutcomeUncertain));
        Assert.Equal(2, Directory.GetFiles(run.ArtifactDirectory, "server-*.json").Length);
    }

    [Fact]
    public async Task IndependentInstancesCanExecuteConcurrently()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Delay = 50;
        await Task.WhenAll(
            runs.RequestAsync(run.RunId, "server", "command", new { }, true, default),
            runs.RequestAsync(run.RunId, "client1", "command", new { }, true, default));
        Assert.Equal(2, pipe.MaxConcurrent);
    }

    [Fact]
    public async Task ArtifactFailureAfterMutationIsReportedAsUncertain()
    {
        var run = await runs.StartAsync("test", 0, default);
        Directory.Delete(run.ArtifactDirectory, true);
        var response = await runs.RequestAsync(run.RunId, "server", "command", new { }, true, default);
        Assert.Equal("artifact_write_failed", response.Error.Code);
        Assert.True(response.Error.OutcomeUncertain);
        Assert.Single(pipe.Methods);
    }

    [Fact]
    public async Task FailedOwnedProcessStopBlocksNewRunUntilCleanupIsRetried()
    {
        var run = await runs.StartAsync("test", 0, default);
        launcher.Processes.Single().FailStop = true;
        Assert.Equal("cleanup_failed", (await runs.StopAsync(run.RunId)).State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runs.StartAsync("test", 0, default));
        launcher.Processes.Single().FailStop = false;
        var stopped = await runs.StopAsync(run.RunId);
        Assert.Equal("stopped", stopped.State);
        Assert.Null(stopped.Error);
        Assert.False(stopped.Instances.Single().ProcessAlive);
    }

    [Fact]
    public async Task StopAllContinuesAfterCompletedRunArtifactsBecomeUnavailable()
    {
        var completed = await runs.StartAsync("test", 0, default);
        await runs.StopAsync(completed.RunId);
        var active = await runs.StartAsync("test", 0, default);
        Directory.Delete(completed.ArtifactDirectory, true);

        var error = await Assert.ThrowsAsync<AggregateException>(() => runs.StopAllAsync());

        Assert.Single(error.InnerExceptions);
        Assert.All(launcher.Processes, process => Assert.False(process.IsAlive));
        Assert.All(launcher.Processes, process => Assert.Equal(1, process.Stops));
        Assert.True(File.Exists(Path.Combine(active.ArtifactDirectory, "run.json")));
    }

    [Fact]
    public async Task WaitReportsFalseWhenAliveButNotReadyAndRejectsUnboundedWaits()
    {
        var run = await runs.StartAsync("test", 0, default);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(run.RunId, "server", "readyForCampaignTests", 1, default));
        Assert.False(result.GetProperty("reached").GetBoolean());
        Assert.True(result.GetProperty("instance").GetProperty("ProcessAlive").GetBoolean());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runs.WaitAsync(run.RunId, "server", "controlReady", 301, default));
        await Assert.ThrowsAsync<ArgumentException>(() => runs.WaitAsync(run.RunId, "server", "invented", 1, default));
    }

    [Fact]
    public async Task WaitUsesEndpointReadinessAndStopArchivesActualLogPathIdempotently()
    {
        string log = Path.Combine(directory, "actual-process-specific.log");
        File.WriteAllText(log, "the log");
        pipe.Status = new { readyForCampaignTests = true, logPath = log };
        var run = await runs.StartAsync("test", 0, default);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(run.RunId, "server", "readyForCampaignTests", 1, default));
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.Equal("the log", (await runs.ReadLogsAsync(run.RunId, "server", null, 100, default)).Text);
        Assert.Equal("stopped", (await runs.StopAsync(run.RunId)).State);
        File.WriteAllText(log, "later unrelated process");
        Assert.Equal("the log", (await runs.ReadLogsAsync(run.RunId, "server", null, 100, default)).Text);
        await runs.StopAsync(run.RunId);
        Assert.Equal(1, launcher.Processes.Single().Stops);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(17)]
    public async Task InvalidClientCountNeverLaunches(int count)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => runs.StartAsync("test", count, default));
        Assert.Empty(launcher.Processes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task InvalidStagedClientIndexRejectsBeforeLaunchAndReleasesLifecycle(int clientIndex)
    {
        var run = await runs.StartAsync("test", 0, default);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runs.StartClientAsync(run.RunId, clientIndex, default));
        Assert.Equal("clientIndex", error.ParamName);
        Assert.Single(launcher.Processes);
        Assert.Equal(new[] { 1 }, preflight.Counts);
        Assert.Equal("launched", (await runs.StartClientAsync(run.RunId, 1, default)).Outcome);
    }

    [Fact]
    public async Task StagedLaunchUsesIncrementalBudgetAndReservesEachSlotOnce()
    {
        var run = await runs.StartAsync("test", 0, default);
        var results = await Task.WhenAll(runs.StartClientAsync(run.RunId, 2, default), runs.StartClientAsync(run.RunId, 2, default));
        Assert.Equal(new[] { "launched", "existing_running" }, results.Select(r => r.Outcome));
        Assert.Equal(2, launcher.Processes.Count);
        Assert.Equal(new[] { 1, 1 }, preflight.Counts);
        Assert.Equal("testclient2", results[0].Run.Instances.Last().Identity.PlatformId);
        Assert.Empty(pipe.Methods);
        await runs.StopAsync(run.RunId);
        Assert.Equal("existing_exited", (await runs.StartClientAsync(run.RunId, 2, default)).Outcome);
        Assert.Equal(2, launcher.Processes.Count);
    }

    [Fact]
    public async Task StagedFailureDoesNotStopServerOrRetryFailedSlot()
    {
        var run = await runs.StartAsync("test", 0, default);
        launcher.FailAt = 2;
        Assert.Equal("launch_failed", (await runs.StartClientAsync(run.RunId, 1, default)).Outcome);
        launcher.FailAt = 0;
        Assert.Equal("launch_failed", (await runs.StartClientAsync(run.RunId, 1, default)).Outcome);
        Assert.True(launcher.Processes.Single().IsAlive);
        Assert.Equal("launched", (await runs.StartClientAsync(run.RunId, 2, default)).Outcome);
    }

    [Fact]
    public async Task PreflightRejectionHasNoLaunchOrSlotReservationAndCanBeRechecked()
    {
        preflight.Allowed = false;
        Assert.Equal("preflight_failed", (await runs.StartAsync("test", 2, default)).State);
        Assert.Empty(launcher.Processes);
        preflight.Allowed = true;
        var run = await runs.StartAsync("test", 0, default);
        preflight.Allowed = false;
        Assert.Equal("preflight_rejected", (await runs.StartClientAsync(run.RunId, 1, default)).Outcome);
        Assert.Single(launcher.Processes);
        preflight.Allowed = true;
        Assert.Equal("launched", (await runs.StartClientAsync(run.RunId, 1, default)).Outcome);
    }

    [Fact]
    public async Task ChangedDeployedBuildBlocksClientAdditionAndJoin()
    {
        var run = await runs.StartAsync("test", 1, default);
        preflight.Mvid = Guid.NewGuid();
        Assert.Equal("bridge_build_changed", (await runs.StartClientAsync(run.RunId, 2, default)).Error.Code);
        var response = await runs.RequestAsync(run.RunId, "client1", "join", new { }, true, default);
        Assert.Equal("bridge_build_changed", response.Error.Code);
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task LoadedBuildMismatchBlocksJoinButMatchingBuildAllowsIt()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Status = new { assemblyMvid = Guid.NewGuid(), protocolVersion = 1 };
        Assert.Equal("bridge_build_mismatch", (await runs.RequestAsync(run.RunId, "client1", "join", new { }, true, default)).Error.Code);
        Assert.DoesNotContain("join", pipe.Methods);
        pipe.Status = new { assemblyMvid = Guid.Empty, protocolVersion = 1 };
        Assert.True((await runs.RequestAsync(run.RunId, "client1", "join", new { }, true, default)).Ok);
        Assert.Equal(1, pipe.Methods.Count(m => m == "join"));
    }

    [Fact]
    public async Task RootExitDoesNotClaimCleanupWhileOwnedDescendantRemains()
    {
        var run = await runs.StartAsync("test", 0, default);
        var process = launcher.Processes.Single();
        process.RetainedDescendant = true;
        Assert.Equal("cleanup_failed", (await runs.StopAsync(run.RunId)).State);
        Assert.False(process.IsAlive);
        Assert.True(process.IsTreeAlive);
        process.RetainedDescendant = false;
        Assert.Equal("stopped", (await runs.StopAsync(run.RunId)).State);
        Assert.Equal(2, process.Stops);
    }

    [Fact]
    public async Task CancelledStagedLaunchBeforeReservationNeverCreatesClient()
    {
        var run = await runs.StartAsync("test", 0, default);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runs.StartClientAsync(run.RunId, 1, cancelled.Token));
        Assert.Single(launcher.Processes);
        Assert.Equal("launched", (await runs.StartClientAsync(run.RunId, 1, default)).Outcome);
    }

    [Fact]
    public async Task SaveSelectionIsValidatedBeforeLaunchAndOnlyPassedToServer()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => runs.StartAsync("test", 1, default, "missing"));
        Assert.Empty(launcher.Processes);
        string path = Path.Combine(directory, "Danustica campaign.sav");
        File.WriteAllText(path, "opaque save bytes");
        var run = await runs.StartAsync("test", 1, default, "Danustica campaign");
        Assert.Equal("Danustica campaign", run.RequestedSave.Name);
        Assert.Equal(new string[] { "Danustica campaign", null }, launcher.Saves);
        Assert.All(run.Instances, i => Assert.Null(i.Status));
        Assert.Equal("opaque save bytes", File.ReadAllText(path));
    }

    public void Dispose() => Directory.Delete(directory, true);

    private sealed class FakeSaveDirectory(string directory) : ISaveDirectoryProvider
    {
        public string GetDirectory() => directory;
        public string GetSessionDirectory() => directory;
    }
    private sealed class FakePreflight : ILaunchPreflight
    {
        public bool Allowed = true;
        public Guid Mvid;
        public List<int> Counts = new();
        public PreflightReport Check(LaunchProfile profile, int count)
        {
            Counts.Add(count);
            return new(Allowed, Allowed ? "ready" : "commit_headroom_insufficient", "fake", count, 8, 2, 2 + (8 * count),
                new CommitSnapshot(1, 1000), new BridgeBuild("fake", Mvid, "1", "staged-ui-capture-v1"));
        }
    }

    private sealed class FakeLauncher : IGameProcessLauncher
    {
        public int FailAt;
        public List<FakeProcess> Processes = new();
        public List<string> Saves = new();
        public IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken, string saveName = null)
        {
            if (Processes.Count + 1 == FailAt) throw new IOException("fake launch failure");
            Saves.Add(saveName);
            var process = new FakeProcess(Processes.Count + 1);
            Processes.Add(process);
            return process;
        }
    }

    private sealed class FakeProcess(int pid) : IOwnedProcess
    {
        public int Pid => pid;
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public bool IsAlive { get; private set; } = true;
        public bool RetainedDescendant;
        public bool IsTreeAlive => IsAlive || RetainedDescendant;
        public int Stops;
        public bool FailStop;
        public Task StopAsync(TimeSpan grace)
        {
            Stops++;
            if (FailStop) throw new IOException("fake stop failure");
            IsAlive = false;
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private sealed class FakePipe : ILiveTestPipeClient
    {
        public ConcurrentQueue<string> Methods = new();
        public bool Throw;
        public bool Uncertain;
        public int Delay;
        public int MaxConcurrent;
        private int concurrent;
        public bool LastMutation;
        public object LastParameters;
        public object Status = new { readyForCampaignTests = false };
        public async Task<LiveTestResponse> SendAsync(InstanceIdentity identity, string method, object parameters, bool mutation, CancellationToken cancellationToken)
        {
            Methods.Enqueue(method);
            LastMutation = mutation;
            LastParameters = parameters;
            int count = Interlocked.Increment(ref concurrent);
            MaxConcurrent = Math.Max(MaxConcurrent, count);
            try
            {
                if (Throw) throw new IOException("fake pipe failure");
                if (Delay > 0) await Task.Delay(Delay, cancellationToken);
                var process = new LiveTestProcessInfo { Pid = identity.Pid, RunToken = identity.RunToken, ProcessStartedUtc = identity.StartedUtc };
                return Uncertain
                    ? LiveTestResponse.Failure(Guid.NewGuid().ToString("N"), process, new LiveTestError("game_thread_timeout", "fake timeout", true))
                    : LiveTestResponse.Success(Guid.NewGuid().ToString("N"), process, JsonSerializer.SerializeToElement(Status));
            }
            finally { Interlocked.Decrement(ref concurrent); }
        }
    }
}
