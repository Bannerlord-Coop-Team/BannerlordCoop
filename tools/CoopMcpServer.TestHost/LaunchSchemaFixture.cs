using Common.LiveTesting;
using System.Text.Json;

namespace CoopMcpServer.TestHost;

// Only in-memory process handles and temporary metadata files, never an OS game launch or live pipe.
public sealed class LaunchSchemaFixture : IMemoryCommitProbe, IBridgeBuildInspector, IGameProcessLauncher, ILiveTestPipeClient, ISaveDirectoryProvider
{
    private readonly string directory;
    private readonly string scenario;
    private int checks;
    private int launches;
    private string Failure => scenario.StartsWith("staged-", StringComparison.Ordinal)
        ? checks > 1 ? scenario.Substring("staged-".Length) : "ready"
        : scenario;

    public LaunchSchemaFixture(string directory, string scenario)
    {
        this.directory = directory;
        this.scenario = scenario;
    }

    public RunOrchestrator CreateOrchestrator()
    {
        string executable = Path.Combine(directory, "Bannerlord.exe");
        File.WriteAllText(executable, "not executable, fixture metadata only");
        var settings = new CoopMcpServerSettings
        {
            ArtifactDirectory = directory,
            Profiles = new() { ["fixture"] = new LaunchProfile { Executable = executable } },
        };
        return new RunOrchestrator(settings, this, this, new IncrementalLogReader(),
            new LaunchPreflight(this, this), new SaveCatalog(this));
    }

    public CommitSnapshot Read()
    {
        checks++;
        if (Failure == "commit_probe_unavailable") throw new IOException("fixture commit probe unavailable");
        return Failure switch
        {
            "commit_probe_invalid" => new CommitSnapshot(-1, 100),
            "commit_headroom_insufficient" => new CommitSnapshot(99, 100),
            _ => new CommitSnapshot(1, long.MaxValue),
        };
    }

    public BridgeBuild Inspect(LaunchProfile profile)
    {
        if (Failure == "bridge_build_unavailable") throw new FileNotFoundException("fixture bridge missing");
        return new BridgeBuild("fixture Coop.dll", Guid.Empty,
            Failure == "missing_markers" ? null : Failure == "wrong_protocol" ? "2" : "1",
            Failure == "missing_markers" ? null : "staged-ui-capture-v1");
    }

    public IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken, string saveName = null)
    {
        if (Failure == "launch_failed") throw new IOException("fixture launch failed");
        return new FixtureProcess(++launches, scenario == "ownership_probe_failed");
    }

    public Task<LiveTestResponse> SendAsync(InstanceIdentity identity, string method, object parameters, bool mutation, CancellationToken cancellationToken) =>
        Task.FromResult(LiveTestResponse.Success("fixture", new LiveTestProcessInfo
        {
            Pid = identity.Pid, ProcessStartedUtc = identity.StartedUtc, Role = identity.Role,
            PlatformId = identity.PlatformId, RunToken = identity.RunToken,
        }, JsonSerializer.SerializeToElement(new { readyForCampaignTests = true })));

    public string GetDirectory() => directory;
    public string GetSessionDirectory() => directory;

    private sealed class FixtureProcess(int pid, bool unavailableTree) : IOwnedProcess
    {
        public int Pid => pid;
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public bool IsAlive { get; private set; } = true;
        public bool IsTreeAlive => unavailableTree && IsAlive ? throw new IOException("fixture ownership probe unavailable") : IsAlive;
        public Task StopAsync(TimeSpan grace) { IsAlive = false; return Task.CompletedTask; }
        public void Dispose() { }
    }
}
