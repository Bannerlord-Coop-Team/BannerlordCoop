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
        runs = new RunOrchestrator(settings, launcher, pipe, new IncrementalLogReader());
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

    public void Dispose() => Directory.Delete(directory, true);

    private sealed class FakeLauncher : IGameProcessLauncher
    {
        public int FailAt;
        public List<FakeProcess> Processes = new();
        public IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken)
        {
            if (Processes.Count + 1 == FailAt) throw new IOException("fake launch failure");
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
        public object Status = new { readyForCampaignTests = false };
        public async Task<LiveTestResponse> SendAsync(InstanceIdentity identity, string method, object parameters, bool mutation, CancellationToken cancellationToken)
        {
            Methods.Enqueue(method);
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
