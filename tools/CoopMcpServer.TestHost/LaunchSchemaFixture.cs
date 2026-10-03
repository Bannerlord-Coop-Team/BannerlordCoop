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

    public BridgeBuild InspectPath(string path) => Inspect(null);

    public BridgeBuild Inspect(LaunchProfile profile)
    {
        if (Failure == "bridge_build_unavailable") throw new FileNotFoundException("fixture bridge missing");
        return new BridgeBuild("fixture Coop.dll", Guid.Empty,
            Failure == "missing_markers" ? null : Failure == "wrong_protocol" ? "2" : "1",
            Failure == "missing_markers" ? null : "staged-ui-capture-v1");
    }

    public IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken, string saveName = null, bool deferClientJoin = false)
    {
        if (Failure == "launch_failed") throw new IOException("fixture launch failed");
        return new FixtureProcess(++launches, scenario == "ownership_probe_failed");
    }

    private readonly List<string> methods = new();
    public Task<LiveTestResponse> SendAsync(InstanceIdentity identity, string method, object parameters, bool mutation, CancellationToken cancellationToken)
    {
        methods.Add(method);
        var process = new LiveTestProcessInfo
        {
            Pid = identity.Pid, ProcessStartedUtc = identity.StartedUtc, Role = identity.Role,
            PlatformId = identity.PlatformId, RunToken = identity.RunToken,
        };
        if (scenario == "drift" && method == "command") return Task.FromResult(DriftStatus(process));
        if (scenario == "control" && method == "command") return Task.FromResult(ControlStatus(process));
        object result = new { readyForCampaignTests = true };
        if (scenario.StartsWith("ui-", StringComparison.Ordinal))
        {
            if (method == "status")
            {
                if (scenario == "ui-status-failure")
                    return Task.FromResult(LiveTestResponse.Failure("exact-status-response", process,
                        new LiveTestError("game_thread_timeout", "fixture status failure", true)));
                result = scenario == "ui-missing" ? new { } : (object)new
                {
                    uiCapability = scenario == "ui-wrong" ? "bounded-ui-layers-v2" : "bounded-ui-layers-v1",
                };
            }
            else result = new { method, parameters, mutation, methods = methods.ToArray() };
        }
        return Task.FromResult(LiveTestResponse.Success("fixture", process, JsonSerializer.SerializeToElement(result)));
    }

    // Bridge-shaped drift-status reply, round-tripped through the real protocol: pending first, then duration_complete.
    private readonly Dictionary<int, int> driftPolls = new();
    private static readonly string[] DriftCombatants = { "4c9d172c-6056-46fe-84f7-4a3c67679538", "7a4bc749-5a35-4f56-be02-79763ad01fb6",
        "c4938032-65ce-4940-96e4-e47d5ff110bb", "eab72560-15c6-4ef7-9de0-8fd36722e196", "3c4ca950-982b-4dff-8ee9-340c5003e01b",
        "d4f77897-d440-4768-9968-d560107f2571", "c4707009-4d2b-41f6-9fd6-b4305dc6604d", "bb8834d7-7287-453b-be0a-15ab35958917",
        "298ad76f-4dba-4e95-af95-6e91efd18c75", "e8a219f9-d6f4-454d-a2fa-38105cdda1a1" };
    private LiveTestResponse DriftStatus(LiveTestProcessInfo process)
    {
        driftPolls[process.Pid] = driftPolls.GetValueOrDefault(process.Pid) + 1;
        bool complete = driftPolls[process.Pid] > 1;
        string json = JsonSerializer.Serialize(new
        {
            incarnation = "72a57da1-88b5-4f7e-9111-71f5c7a965f4", operationId = "ea451533-21af-47ce-b889-887ee8da5e71", localController = "testclient" + (process.Pid - 1),
            requestedSeconds = 30, sampledSpanSeconds = complete ? 30.0 : 12.5, sampleCount = complete ? 601 : 251, maxSampleGapSeconds = 0.05, gapsOver250ms = 0,
            ended = complete ? "duration_complete" : null, completeCoverage = complete,
            rows = DriftCombatants.Select((id, index) => new { slot = index / 5, combatantId = id, kind = index % 5 == 0 ? "helm" : "oar", valid = complete ? 601 : 251, invalid = 0, occupancyLoss = 0 }).ToArray(),
        });
        return CommandReply(process, "coop.debug.naval_lab.drift-status", json);
    }

    private static LiveTestResponse CommandReply(LiveTestProcessInfo process, string name, string json)
    {
        var response = LiveTestResponse.Success(Guid.NewGuid().ToString("N"), process, new
        {
            name, arguments = Array.Empty<string>(), found = true, output = "LIVE_TEST_JSON=" + json,
            succeeded = true, errorCode = (string)null, hasStructuredResult = true, structuredResult = JsonSerializer.Deserialize<JsonElement>(json),
        });
        LiveTestProtocol.TryDeserializeResponse(LiveTestProtocol.SerializeResponse(response), out var wire, out _);
        return wire;
    }

    // Bridge-shaped control-status per owned slot: previous state, then the operation pending, then completed; pulse before neutral.
    private readonly Dictionary<int, int> controlPolls = new();
    private static readonly string[][] ControlOperations = { new[] { "6d58832a-f320-4d32-aed7-29a7b4679622", "e65f0312-c6a5-4d46-9b80-98bae39c72c2" },
        new[] { "b98d8246-831c-446d-b686-8cc1874cea62", "158bbdfc-8c80-4587-a15c-8058e0b189cf" } };
    private LiveTestResponse ControlStatus(LiveTestProcessInfo process)
    {
        int slot = process.Pid - 2, poll = controlPolls[process.Pid] = controlPolls.GetValueOrDefault(process.Pid) + 1;
        int operation = Math.Min((poll - 1) / 3, 1), stage = (poll - 1) % 3;
        bool previous = stage == 0, neutralOperation = operation == 1;
        string id = previous ? (neutralOperation ? ControlOperations[slot][0] : "00000000-0000-0000-0000-000000000000") : ControlOperations[slot][operation];
        string phase = previous && !neutralOperation ? "not_requested" : stage == 1 ? "pending_synthetic_axes" : "completed_axes_neutral_requested";
        long first = id.StartsWith("0000", StringComparison.Ordinal) ? 0 : neutralOperation && !previous ? 1902 : 1871;
        long last = first == 0 ? 0 : first + (neutralOperation && !previous ? 28 : 29), neutral = phase == "completed_axes_neutral_requested" ? last + 1 : 0;
        float lateral = slot == 0 ? 0.5f : -0.5f;
        bool pulseApplied = id == ControlOperations[slot][0] && phase != "not_requested";
        var application = new { rudder = pulseApplied ? lateral * 1.4f : 0f, lateral = pulseApplied ? (slot == 0 ? -1 : 1) : 0, longitudinal = pulseApplied ? 1 : 0,
            doubleTap = pulseApplied ? 1 : 0, sail = 0 };
        string json = JsonSerializer.Serialize(new
        {
            incarnation = "72a57da1-88b5-4f7e-9111-71f5c7a965f4", mode = "TwoClientNative", host = new { HostControllerId = "testclient1", Epoch = 1 },
            status = new
            {
                IncarnationId = "72a57da1-88b5-4f7e-9111-71f5c7a965f4", epoch = 1, owner = "testclient" + (slot + 1), ship = slot, ready = true, terminal = false,
                blocked = false, inputBlocker = (string)null, operationId = id, phase, pulseSynthetic = true, pulseFirstInputSequence = first,
                pulseLastInputSequence = last, pulseNeutralInputSequence = neutral, requestedLateralAxis = neutralOperation && !previous ? 0f : lateral,
                requestedForwardAxis = neutralOperation && !previous ? 0f : 1f, remainingSeconds = stage == 1 ? 0.5 : 0.0,
                currentOwnerApplication = slot == 0 ? new object[] { application, null } : new object[] { null, application }, unavailable = (string)null,
            },
            unavailable = (string)null,
        });
        return CommandReply(process, "coop.debug.naval_lab.control-status", json);
    }

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
