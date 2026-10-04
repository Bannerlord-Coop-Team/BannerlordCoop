using Common.LiveTesting;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoopMcpServer;

public interface IRunOrchestrator
{
    Task<RunView> StartAsync(string profile, int clientCount, CancellationToken cancellationToken, string saveName = null);
    PreflightReport Preflight(string profile, int clientCount);
    SavePage ListSaves(string profile, int offset);
    Task<ClientLaunchView> StartClientAsync(string runId, int clientIndex, CancellationToken cancellationToken);
    Task<RunView> GetAsync(string runId, CancellationToken cancellationToken);
    Task<object> WaitAsync(string runId, string instance, string state, int timeoutSeconds, CancellationToken cancellationToken);
    Task<object> WaitForDriftAsync(string runId, string instance, string operationId, string incarnation, int timeoutSeconds, CancellationToken cancellationToken);
    Task<object> WaitForControlAsync(string runId, string instance, string operationId, string incarnation, int slot, bool requireNeutral, int timeoutSeconds, CancellationToken cancellationToken);
    Task<object> WaitForLabAsync(string runId, string incarnation, string stage, int timeoutSeconds, CancellationToken cancellationToken);
    Task<LiveTestResponse> RequestAsync(string runId, string instance, string method, object parameters, bool mutation, CancellationToken cancellationToken);
    Task<LogChunk> ReadLogsAsync(string runId, string instance, string cursor, int maxBytes, CancellationToken cancellationToken);
    Task<RunView> StopAsync(string runId);
    Task StopAllAsync();
    string ScreenshotPath(string runId, string instance);
    VideoTarget ResolveVideoTarget(string runId, string instance);
}

public sealed record VideoTarget(string VideoPath, string ContactSheetPath, int Pid, bool ProcessAlive);

#nullable enable annotations
public sealed record InstanceView(string Name, InstanceIdentity Identity, bool ProcessAlive,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] LiveTestError? Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool? ProcessTreeAlive, bool CleanupComplete,
    StartupPopupView StartupPopup = null);
public sealed record RunView(string RunId, string Profile, string ArtifactDirectory, string State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Error, InstanceView[] Instances,
    PreflightReport Preflight, IReadOnlyDictionary<int, string> ClientAttempts,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] SaveEntry? RequestedSave, IReadOnlyDictionary<int, LiveTestError> ClientLaunchErrors);
public sealed record ClientLaunchView(string Outcome, string Instance, RunView Run,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] LiveTestError? Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] PreflightReport? Preflight);
#nullable restore annotations

public sealed partial class RunOrchestrator : IRunOrchestrator, IDeploymentRunGuard
{
    private readonly CoopMcpServerSettings settings;
    private readonly IGameProcessLauncher launcher;
    private readonly ILiveTestPipeClient pipe;
    private readonly IIncrementalLogReader logs;
    private readonly ILaunchPreflight preflight;
    private readonly ISaveCatalog saves;
    private readonly IDeploymentLease deploymentLease;
    private readonly IBuildCleanupRecovery buildCleanup;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly ConcurrentDictionary<string, Run> runs = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private sealed class Instance
    {
        public string Name;
        public IOwnedProcess Process;
        public InstanceIdentity Identity;
        public SemaphoreSlim Gate = new(1, 1);
        public JsonElement? Status;
        public LiveTestError Error;
        public string LogPath;
        public string ArchivedLogPath;
        public bool Stopped;
        public bool CleanupComplete;
        public StartupPopupView StartupPopup;
        public bool StartupPopupActionAttempted;
    }

    private sealed class Run
    {
        public string Id;
        public string Profile;
        public string Directory;
        public string State = "started";
        public object ArtifactGate = new();
        public string Error;
        public List<Instance> Instances = new();
        public PreflightReport Preflight;
        public SaveEntry RequestedSave;
        public ConcurrentDictionary<int, string> ClientAttempts = new();
        public ConcurrentDictionary<int, LiveTestError> ClientLaunchErrors = new();
    }

    public RunOrchestrator(CoopMcpServerSettings settings, IGameProcessLauncher launcher,
        ILiveTestPipeClient pipe, IIncrementalLogReader logs, ILaunchPreflight preflight, ISaveCatalog saves, IDeploymentLease deploymentLease = null, IBuildCleanupRecovery buildCleanup = null)
    {
        this.settings = settings;
        this.launcher = launcher;
        this.pipe = pipe;
        this.logs = logs;
        this.preflight = preflight;
        this.saves = saves;
        this.deploymentLease = deploymentLease;
        this.buildCleanup = buildCleanup;
    }

    public async Task<DeploymentReport> DeployAsync(Func<Task<DeploymentReport>> action, CancellationToken cancellationToken)
    {
        if (!await lifecycle.WaitAsync(0, cancellationToken)) throw new InvalidOperationException("A launch, stop or deployment is already in progress.");
        try
        {
            if (buildCleanup != null) await buildCleanup.RecoverAsync();
            if (runs.Values.Any(r => r.State != "stopped" && r.State != "launch_failed" && r.State != "preflight_failed"))
                throw new InvalidOperationException("Stop the owned run and confirm cleanup before deploying.");
            return await action();
        }
        finally { lifecycle.Release(); }
    }

    public async Task<RunView> StartAsync(string profile, int clientCount, CancellationToken cancellationToken, string saveName = null)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (buildCleanup != null) await buildCleanup.RecoverAsync();
            if (runs.Values.Any(r => r.State != "stopped" && r.State != "launch_failed" && r.State != "preflight_failed"))
                throw new InvalidOperationException("Stop the active run before starting another run.");
            if (!settings.Profiles.TryGetValue(profile, out var launchProfile))
                throw new ArgumentException("Unknown configured profile.");
            launchProfile.Validate(clientCount);
            using var deployment = deploymentLease?.Acquire(launchProfile);
            var selectedSave = saveName == null ? null : saves.ValidateSelection(saveName);
            var run = new Run { RequestedSave = selectedSave, Id = Guid.NewGuid().ToString("N"), Profile = profile, Preflight = preflight.Check(launchProfile, clientCount + 1) };
            run.Directory = Path.Combine(settings.ArtifactDirectory, run.Id);
            Directory.CreateDirectory(run.Directory);
            runs[run.Id] = run;
            try
            {
                if (!run.Preflight.Allowed)
                {
                    run.State = "preflight_failed";
                    run.Error = run.Preflight.Message;
                    Save(run);
                    return View(run);
                }
                Save(run);
                // Start every requested process before observing readiness so their boot work overlaps.
                for (int index = 0; index <= clientCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (index > 0) run.ClientAttempts[index] = "outcome_unknown";
                    LaunchInstance(run, launchProfile, index, deferClientJoin: false);
                    if (index > 0) run.ClientAttempts[index] = "launched";
                    Save(run);
                }
            }
            catch (Exception exception)
            {
                foreach (var attempt in run.ClientAttempts.Where(a => a.Value == "outcome_unknown"))
                    run.ClientAttempts[attempt.Key] = "launch_failed";
                run.Error = exception.Message;
                await StopInstancesAsync(run);
                run.State = Instances(run).All(i => i.CleanupComplete) ? "launch_failed" : "cleanup_failed";
                Save(run);
            }
            return View(run);
        }
        finally { lifecycle.Release(); }
    }

    public SavePage ListSaves(string profile, int offset)
    {
        if (!settings.Profiles.TryGetValue(profile, out var launchProfile)) throw new ArgumentException("Unknown configured profile.");
        launchProfile.Validate(0);
        return saves.List(offset);
    }

    public PreflightReport Preflight(string profile, int clientCount)
    {
        if (!settings.Profiles.TryGetValue(profile, out var launchProfile)) throw new ArgumentException("Unknown configured profile.");
        launchProfile.Validate(clientCount);
        return preflight.Check(launchProfile, clientCount + 1);
    }

    public async Task<ClientLaunchView> StartClientAsync(string runId, int clientIndex, CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (buildCleanup != null) await buildCleanup.RecoverAsync();
            var run = FindRun(runId);
            string name = "client" + clientIndex;
            var profile = settings.Profiles[run.Profile];
            using var deployment = deploymentLease?.Acquire(profile);
            ArgumentOutOfRangeException.ThrowIfLessThan(clientIndex, 1);
            profile.Validate(clientIndex);
            if (run.ClientAttempts.TryGetValue(clientIndex, out string attempt))
            {
                var existing = Instances(run).SingleOrDefault(i => i.Name == name);
                string outcome = existing != null ? (Alive(existing) ? "existing_running" : "existing_exited") : attempt;
                run.ClientLaunchErrors.TryGetValue(clientIndex, out var launchError);
                return new ClientLaunchView(outcome, name, View(run), existing?.Error ?? launchError, null);
            }
            if (run.State != "started" || !Alive(FindInstance(run, "server")))
                return new ClientLaunchView("rejected", name, View(run), new LiveTestError("run_not_active", "A live owned server is required; start_client never restarts a run.", false), null);
            var report = preflight.Check(profile, 1);
            if (!report.Allowed || report.Bridge.Mvid != run.Preflight.Bridge.Mvid)
                return new ClientLaunchView("preflight_rejected", name, View(run), new LiveTestError(
                    report.Allowed ? "bridge_build_changed" : report.Code, report.Allowed ? "Coop.dll changed since this run started. Stop and start a new run." : report.Message, false), report);
            cancellationToken.ThrowIfCancellationRequested();
            run.ClientAttempts[clientIndex] = "outcome_unknown";
            Save(run);
            try
            {
                LaunchInstance(run, profile, clientIndex, deferClientJoin: true);
                run.ClientAttempts[clientIndex] = "launched";
            }
            catch (Exception e)
            {
                run.ClientAttempts[clientIndex] = "launch_failed";
                run.ClientLaunchErrors[clientIndex] = new LiveTestError("client_launch_failed", e.Message, false);
                Save(run);
                return new ClientLaunchView("launch_failed", name, View(run), run.ClientLaunchErrors[clientIndex], report);
            }
            Save(run);
            return new ClientLaunchView("launched", name, View(run), null, report);
        }
        finally { lifecycle.Release(); }
    }

    // Retain ownership immediately, regardless of whether the client joins automatically or later.
    private void LaunchInstance(Run run, LaunchProfile profile, int index, bool deferClientJoin)
    {
        string role = index == 0 ? "server" : "client";
        string platformId = index == 0 ? profile.ServerPlatformId : profile.ClientPlatformIds[index - 1];
        IOwnedProcess process = launcher.Launch(profile, role, platformId, run.Id, index == 0 ? run.RequestedSave?.Name : null, deferClientJoin);
        lock (run.ArtifactGate)
            run.Instances.Add(new Instance
            {
                Name = index == 0 ? "server" : "client" + index,
                Process = process,
                Identity = new InstanceIdentity(process.Pid, process.StartedUtc, role, platformId, run.Id),
            });
    }

    public async Task<RunView> GetAsync(string runId, CancellationToken cancellationToken)
    {
        Run run = FindRun(runId);
        await Task.WhenAll(Instances(run).Select(i => RefreshAsync(run, i, cancellationToken)));
        Save(run);
        return View(run);
    }

    public async Task<object> WaitAsync(string runId, string instance, string state, int timeoutSeconds, CancellationToken cancellationToken)
    {
        string[] states = { "controlReady", "readyToJoin", "commandRegistryReady", "readyForCampaignTests", "readyForMissionTests", "exited" };
        if (!states.Contains(state)) throw new ArgumentException("state must be one of: " + string.Join(", ", states));
        if (timeoutSeconds < 1 || timeoutSeconds > 300) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "timeout_seconds must be 1..300.");
        var run = FindRun(runId);
        var target = FindInstance(run, instance);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        bool reached = false;
        bool deadlineExpired = false;
        try
        {
            do
            {
                await RefreshAsync(run, target, timeout.Token);
                reached = Matches(target, state);
                if (reached && state == "readyForCampaignTests" && target.Identity.Role == "client")
                    await TryDismissStartupPopupAsync(run, target, timeout.Token);
                if (reached || !Alive(target)) break;
                await Task.Delay(500, timeout.Token);
            } while (true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { deadlineExpired = true; }
        Save(run);
        return new { reached, state, outcome = reached ? "reached" : !Alive(target) ? "process_exited" : deadlineExpired ? "deadline_expired" : "not_ready",
            deadlineExpired, lastError = target.Error, instance = View(target) };
    }

    public async Task<object> WaitForDriftAsync(string runId, string instance, string operationId, string incarnation, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(operationId, out var operation) || operation == Guid.Empty) throw new ArgumentException("operation_id must be a nonempty UUID.", nameof(operationId));
        if (!Guid.TryParse(incarnation, out var fixture) || fixture == Guid.Empty) throw new ArgumentException("incarnation must be a nonempty UUID.", nameof(incarnation));
        if (timeoutSeconds < 1 || timeoutSeconds > 90) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "timeout_seconds must be 1..90.");
        var run = FindRun(runId);
        var target = FindInstance(run, instance);
        if (target.Identity.Role != "client") throw new ArgumentException("Drift recordings are client-owned; use client1, client2, etc.");
        var wait = await PollClientCommandAsync(run, target, "coop.debug.naval_lab.drift-status", timeoutSeconds, 500,
            drift => ClassifyDrift(drift, operation, fixture), cancellationToken);
        return new { outcome = wait.Outcome, reached = wait.Outcome == "ended", ended = wait.Outcome == "ended" ? wait.Structured.GetProperty("ended").GetString() : null,
            deadlineExpired = wait.DeadlineExpired, polls = wait.Polls, elapsedMs = wait.ElapsedMs, lastError = wait.Response?.Error, responseId = wait.Response?.Id,
            last = wait.DeadlineExpired && wait.HasStructured ? wait.Structured.EnumerateObject().Where(p => p.Name is "operationId" or "incarnation" or "sampledSpanSeconds" or "sampleCount")
                .ToDictionary(p => p.Name, p => p.Value.Clone()) : null,
            response = wait.DeadlineExpired ? null : wait.Response };
    }

    public async Task<object> WaitForControlAsync(string runId, string instance, string operationId, string incarnation, int slot, bool requireNeutral, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(operationId, out var operation) || operation == Guid.Empty) throw new ArgumentException("operation_id must be a nonempty UUID.", nameof(operationId));
        if (!Guid.TryParse(incarnation, out var fixture) || fixture == Guid.Empty) throw new ArgumentException("incarnation must be a nonempty UUID.", nameof(incarnation));
        if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException(nameof(slot), "slot must be 0 or 1.");
        if (timeoutSeconds < 1 || timeoutSeconds > 30) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "timeout_seconds must be 1..30.");
        var run = FindRun(runId);
        var target = FindInstance(run, instance);
        if (target.Identity.Role != "client") throw new ArgumentException("Control pulses are observed on the owning client; use client1, client2, etc.");
        var wait = await PollClientCommandAsync(run, target, "coop.debug.naval_lab.control-status", timeoutSeconds, 250,
            control => ClassifyControl(control, operation, fixture, slot, requireNeutral), cancellationToken);
        JsonElement status = default;
        bool hasStatus = wait.HasStructured && wait.Structured.TryGetProperty("status", out status) && status.ValueKind == JsonValueKind.Object;
        return new { outcome = wait.Outcome, reached = wait.Outcome == "completed",
            phase = hasStatus && status.TryGetProperty("phase", out var phase) && phase.ValueKind == JsonValueKind.String ? phase.GetString() : null,
            deadlineExpired = wait.DeadlineExpired, polls = wait.Polls, elapsedMs = wait.ElapsedMs, lastError = wait.Response?.Error, responseId = wait.Response?.Id,
            last = wait.DeadlineExpired && hasStatus ? ControlSummary(status, slot) : null,
            response = wait.DeadlineExpired ? null : wait.Response };
    }

    public async Task<object> WaitForLabAsync(string runId, string incarnation, string stage, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(incarnation, out var fixture) || fixture == Guid.Empty) throw new ArgumentException("incarnation must be a nonempty UUID.", nameof(incarnation));
        if (stage != "deployable" && stage != "controls_ready") throw new ArgumentException("stage must be deployable or controls_ready.", nameof(stage));
        if (timeoutSeconds < 1 || timeoutSeconds > 30) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "timeout_seconds must be 1..30.");
        var run = FindRun(runId);
        // The server coordinator aggregates every owner's scene/ready reports and the committed native stations.
        var target = FindInstance(run, "server");
        var wait = await PollClientCommandAsync(run, target, "coop.debug.naval_lab.inspect", timeoutSeconds, 250,
            inspect => ClassifyLab(inspect, fixture, stage == "controls_ready"), cancellationToken);
        return new { outcome = wait.Outcome, reached = wait.Outcome == "reached", stage,
            deadlineExpired = wait.DeadlineExpired, polls = wait.Polls, elapsedMs = wait.ElapsedMs, lastError = wait.Response?.Error, responseId = wait.Response?.Id,
            last = wait.HasStructured ? LabSummary(wait.Structured) : null,
            response = wait.DeadlineExpired ? null : wait.Response };
    }

    private sealed record CommandWait(string Outcome, LiveTestResponse Response, bool HasStructured, JsonElement Structured, bool DeadlineExpired, int Polls, long ElapsedMs);

    // Polls one read-only instance command off the game thread; the instance gate is held per poll, never across the delay.
    private async Task<CommandWait> PollClientCommandAsync(Run run, Instance target, string command, int timeoutSeconds, int delayMilliseconds,
        Func<JsonElement, string> classify, CancellationToken cancellationToken)
    {
        long started = Environment.TickCount64;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        LiveTestResponse response = null;
        string outcome;
        int polls = 0;
        try
        {
            while (true)
            {
                await target.Gate.WaitAsync(timeout.Token);
                try
                {
                    if (!Alive(target)) { outcome = "process_exited"; break; }
                    polls++;
                    var reply = await pipe.SendAsync(target.Identity, "command", new { name = command, arguments = Array.Empty<string>() }, false, timeout.Token);
                    // A transport failure caused by this wait's own deadline is the deadline, not a bridge fault.
                    if (!reply.Ok) timeout.Token.ThrowIfCancellationRequested();
                    response = reply;
                }
                finally { target.Gate.Release(); }
                outcome = !response.Ok ? (Alive(target) ? "fault" : "process_exited")
                    : response.Result is not JsonElement result || result.ValueKind != JsonValueKind.Object ||
                        !result.TryGetProperty("succeeded", out var succeeded) || succeeded.ValueKind != JsonValueKind.True ? "fault"
                    : !TryGetStructuredResult(response, out var structured) ? "malformed"
                    : classify(structured);
                if (outcome != null) break;
                await Task.Delay(delayMilliseconds, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { outcome = "deadline_expired"; }
        bool deadlineExpired = outcome == "deadline_expired";
        JsonElement final = default;
        bool hasStructured = response != null && TryGetStructuredResult(response, out final);
        if (response != null)
        {
            var recorded = RecordResponse(run, target, response, false);
            if (recorded != response) outcome = "fault";
            response = recorded;
        }
        Save(run);
        return new CommandWait(outcome, response, hasStructured, final, deadlineExpired, polls, Environment.TickCount64 - started);
    }

    // Null means the requested recording is still running; any other value ends the wait.
    private static string ClassifyDrift(JsonElement drift, Guid operation, Guid incarnation)
    {
        if (drift.TryGetProperty("unavailable", out var unavailable) && unavailable.ValueKind != JsonValueKind.Null) return "unavailable";
        if (!TryGetGuid(drift, "operationId", out var actualOperation) || !TryGetGuid(drift, "incarnation", out var actualIncarnation) || !drift.TryGetProperty("ended", out var ended) ||
            (ended.ValueKind != JsonValueKind.Null && ended.ValueKind != JsonValueKind.String)) return "malformed";
        if (actualOperation != operation || actualIncarnation != incarnation) return "identity_mismatch";
        return ended.ValueKind == JsonValueKind.String ? "ended" : null;
    }

    // Null means pending: an earlier or not yet received operation, the pulse still running, or neutral input not yet applied.
    private static string ClassifyControl(JsonElement control, Guid operation, Guid incarnation, int slot, bool requireNeutral)
    {
        if (control.TryGetProperty("unavailable", out var unavailable) && unavailable.ValueKind != JsonValueKind.Null) return "unavailable";
        if (!TryGetGuid(control, "incarnation", out var fixture) || !control.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object ||
            !TryGetGuid(status, "IncarnationId", out var statusFixture) || !TryGetGuid(status, "operationId", out var actualOperation) ||
            !status.TryGetProperty("ship", out var ship) || ship.ValueKind != JsonValueKind.Number ||
            !status.TryGetProperty("phase", out var phase) || phase.ValueKind != JsonValueKind.String) return "malformed";
        if (fixture != incarnation || statusFixture != incarnation || !ship.TryGetInt32(out int actualSlot) || actualSlot != slot) return "identity_mismatch";
        bool Is(JsonElement value, string name, JsonValueKind kind) => value.TryGetProperty(name, out var field) && field.ValueKind == kind;
        bool EpochOne(JsonElement value, string name) => value.TryGetProperty(name, out var epoch) && epoch.ValueKind == JsonValueKind.Number && epoch.TryGetInt32(out int number) && number == 1;
        if (!control.TryGetProperty("host", out var host) || host.ValueKind != JsonValueKind.Object || !EpochOne(host, "Epoch") || !EpochOne(status, "epoch") ||
            !Is(status, "ready", JsonValueKind.True) || !Is(status, "terminal", JsonValueKind.False) || !Is(status, "blocked", JsonValueKind.False) ||
            !Is(status, "inputBlocker", JsonValueKind.Null) || (status.TryGetProperty("unavailable", out var statusUnavailable) && statusUnavailable.ValueKind != JsonValueKind.Null))
            return "ineligible";
        if (actualOperation != operation || phase.GetString() == "pending_synthetic_axes") return null;
        if (phase.GetString() != "completed_axes_neutral_requested") return "control_failed";
        long Sequence(string name) => status.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : -1;
        long first = Sequence("pulseFirstInputSequence"), last = Sequence("pulseLastInputSequence"), neutral = Sequence("pulseNeutralInputSequence");
        if (first <= 0 || last < first || neutral <= last) return "malformed";
        if (!requireNeutral) return "completed";
        var application = OwnApplication(status, slot);
        bool Zero(string name) => application is JsonElement own && own.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.GetDouble() == 0;
        return Zero("rudder") && Zero("longitudinal") ? "completed" : null;
    }

    // Null means pending: scene/ready reports, the epoch-one host or committed native controls are still arriving.
    private static string ClassifyLab(JsonElement inspect, Guid incarnation, bool requireControls)
    {
        if (!inspect.TryGetProperty("manifest", out var manifest)) return "malformed";
        if (manifest.ValueKind == JsonValueKind.Null) return "unavailable";
        if (manifest.ValueKind != JsonValueKind.Object || !TryGetGuid(manifest, "IncarnationId", out var actual) ||
            !manifest.TryGetProperty("Controllers", out var controllers) || controllers.ValueKind != JsonValueKind.Array) return "malformed";
        if (actual != incarnation) return "identity_mismatch";
        if (!inspect.TryGetProperty("failure", out var failure) || (failure.ValueKind != JsonValueKind.Null && failure.ValueKind != JsonValueKind.String)) return "malformed";
        if (failure.ValueKind == JsonValueKind.String) return "fixture_failed";
        if (!inspect.TryGetProperty("ready", out var ready) || ready.ValueKind != JsonValueKind.Array ||
            !inspect.TryGetProperty("sceneReady", out var sceneReady) || sceneReady.ValueKind != JsonValueKind.Array ||
            !inspect.TryGetProperty("host", out var host) || (host.ValueKind != JsonValueKind.Null && host.ValueKind != JsonValueKind.Object)) return "malformed";
        if (host.ValueKind == JsonValueKind.Object && (!host.TryGetProperty("Epoch", out var epoch) || epoch.ValueKind != JsonValueKind.Number ||
            !epoch.TryGetInt32(out int number) || number != 1)) return "ineligible";
        bool All(JsonElement reports) => controllers.EnumerateArray().All(id => id.ValueKind == JsonValueKind.String &&
            reports.EnumerateArray().Any(r => r.ValueKind == JsonValueKind.String && r.GetString() == id.GetString()));
        if (host.ValueKind == JsonValueKind.Null || controllers.GetArrayLength() == 0 || !All(sceneReady) || !All(ready)) return null;
        if (!requireControls) return "reached";
        if (!inspect.TryGetProperty("nativeControls", out var controls) || controls.ValueKind != JsonValueKind.Object) return "ineligible";
        return controls.TryGetProperty("ready", out var controlsReady) && controlsReady.ValueKind == JsonValueKind.True ? "reached" : null;
    }

    private static Dictionary<string, object> LabSummary(JsonElement inspect)
    {
        JsonElement? Field(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) ? field.Clone() : null;
        var summary = new Dictionary<string, object>
        {
            ["ready"] = Field(inspect, "ready"), ["sceneReady"] = Field(inspect, "sceneReady"), ["failure"] = Field(inspect, "failure"),
            ["hostEpoch"] = Field(inspect, "host") is JsonElement host ? Field(host, "Epoch") : null,
        };
        if (Field(inspect, "manifest") is JsonElement manifest)
        {
            summary["incarnation"] = Field(manifest, "IncarnationId");
            summary["controllers"] = Field(manifest, "Controllers");
        }
        if (Field(inspect, "nativeControls") is JsonElement controls)
            summary["nativeControls"] = new { ready = Field(controls, "ready"), stationsCommitted = Field(controls, "stationsCommitted"), nativeReleaseSent = Field(controls, "nativeReleaseSent") };
        return summary;
    }

    private static JsonElement? OwnApplication(JsonElement status, int slot) =>
        status.TryGetProperty("currentOwnerApplication", out var applications) && applications.ValueKind == JsonValueKind.Array &&
        applications.GetArrayLength() > slot && applications[slot].ValueKind == JsonValueKind.Object ? applications[slot] : null;

    private static Dictionary<string, JsonElement> ControlSummary(JsonElement status, int slot)
    {
        var summary = status.EnumerateObject().Where(p => p.Name is "operationId" or "phase" or "remainingSeconds" or "pulseFirstInputSequence"
            or "pulseLastInputSequence" or "pulseNeutralInputSequence").ToDictionary(p => p.Name, p => p.Value.Clone());
        if (OwnApplication(status, slot) is JsonElement application) summary["application"] = application.Clone();
        return summary;
    }

    private static bool TryGetGuid(JsonElement value, string name, out Guid id)
    {
        id = Guid.Empty;
        return value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String && Guid.TryParse(field.GetString(), out id);
    }

    private static bool TryGetStructuredResult(LiveTestResponse response, out JsonElement structured)
    {
        structured = default;
        if (response.Result is not JsonElement result || result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("hasStructuredResult", out var has) || has.ValueKind != JsonValueKind.True ||
            !result.TryGetProperty("structuredResult", out structured) || structured.ValueKind != JsonValueKind.Object) return false;
        return true;
    }

    public async Task<LiveTestResponse> RequestAsync(string runId, string instance, string method, object parameters,
        bool mutation, CancellationToken cancellationToken)
    {
        var run = FindRun(runId);
        var target = FindInstance(run, instance);
        await target.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!Alive(target)) throw new InvalidOperationException("The owned process has exited.");
            if (method == "join")
            {
                if (target.Identity.Role != "client") return LocalFailure(target, "client_only", "Only an owned client can join.");
                var report = preflight.Check(settings.Profiles[run.Profile], 0);
                if (!report.Allowed || report.Bridge.Mvid != run.Preflight.Bridge.Mvid)
                    return LocalFailure(target, report.Allowed ? "bridge_build_changed" : report.Code,
                        report.Allowed ? "Coop.dll changed since launch; no join was sent." : report.Message);
                var mismatch = await CheckLoadedBridgeAsync(target, report.Bridge.Mvid, cancellationToken);
                if (mismatch != null) return mismatch;
                var server = FindInstance(run, "server");
                await server.Gate.WaitAsync(cancellationToken);
                try
                {
                    if (!Alive(server)) return LocalFailure(server, "server_exited", "The owned server exited; no join was sent.");
                    mismatch = await CheckLoadedBridgeAsync(server, report.Bridge.Mvid, cancellationToken);
                    if (mismatch != null) return mismatch;
                }
                finally { server.Gate.Release(); }
            }
            if (method == "ui-layers" || (method == "ui-inspect" &&
                JsonSerializer.SerializeToElement(parameters).TryGetProperty("layer", out var layer) && layer.ValueKind != JsonValueKind.Null))
            {
                var status = await pipe.SendAsync(target.Identity, "status", new { }, false, cancellationToken);
                if (!status.Ok) return status;
                if (status.Result is not JsonElement value || value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty("uiCapability", out var capability) || capability.ValueKind != JsonValueKind.String ||
                    capability.GetString() != "bounded-ui-layers-v1")
                    return LocalFailure(target, "ui_capability_unavailable", "Loaded bridge lacks bounded-ui-layers-v1; no UI request was sent.");
            }
            var response = await pipe.SendAsync(target.Identity, method, parameters, mutation, cancellationToken);
            return RecordResponse(run, target, response, mutation);
        }
        finally { target.Gate.Release(); }
    }

    private LiveTestResponse RecordResponse(Run run, Instance target, LiveTestResponse response, bool mutation)
    {
        // Write each result before returning it so uncertain mutations remain inspectable after MCP disconnects.
        try
        {
            File.WriteAllText(Path.Combine(run.Directory, target.Name + "-" + response.Id + ".json"),
                LiveTestProtocol.SerializeResponse(response));
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            var failure = LiveTestResponse.Failure(response.Id, response.Process,
                new LiveTestError("artifact_write_failed", exception.Message, mutation || (response.Error?.OutcomeUncertain ?? false)));
            failure.Result = new { bridgeResponse = response };
            return failure;
        }
        return response;
    }

    public async Task<LogChunk> ReadLogsAsync(string runId, string instance, string cursor, int maxBytes, CancellationToken cancellationToken)
    {
        var run = FindRun(runId);
        var target = FindInstance(run, instance);
        if (target.LogPath == null && !target.Stopped) await RefreshAsync(run, target, cancellationToken);
        await target.Gate.WaitAsync(cancellationToken);
        try
        {
            string path = target.ArchivedLogPath ?? (Alive(target) ? target.LogPath : null);
            if (path == null) throw new InvalidOperationException("No endpoint-reported log path is available. Query status while the endpoint is alive first.");
            return logs.Read(path, cursor, maxBytes);
        }
        finally { target.Gate.Release(); }
    }

    public string ScreenshotPath(string runId, string instance)
    {
        var run = FindRun(runId);
        FindInstance(run, instance);
        return Path.Combine(run.Directory, instance + "-" + Guid.NewGuid().ToString("N") + ".bmp");
    }

    // The owned process handle stays open until stop, so its PID cannot be reused while it is alive.
    public VideoTarget ResolveVideoTarget(string runId, string instance)
    {
        var run = FindRun(runId);
        var target = FindInstance(run, instance);
        string stem = Path.Combine(run.Directory, instance + "-video-" + Guid.NewGuid().ToString("N"));
        return new VideoTarget(stem + ".mp4", stem + "-sheet.png", target.Identity.Pid, Alive(target));
    }

    public async Task<RunView> StopAsync(string runId)
    {
        await lifecycle.WaitAsync();
        try
        {
            var run = FindRun(runId);
            await StopInstancesAsync(run);
            run.State = Instances(run).All(i => i.CleanupComplete) ? "stopped" : "cleanup_failed";
            if (run.State == "stopped") run.Error = null;
            Save(run);
            return View(run);
        }
        finally { lifecycle.Release(); }
    }

    public async Task StopAllAsync()
    {
        var failures = new List<Exception>();
        foreach (var run in runs.Values)
        {
            try
            {
                var stopped = await StopAsync(run.Id);
                if (stopped.State != "stopped") throw new IOException("Run " + run.Id + " cleanup failed: " + stopped.Error);
            }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (failures.Count > 0) throw new AggregateException("One or more runs failed cleanup.", failures);
    }

    private async Task StopInstancesAsync(Run run)
    {
        await Task.WhenAll(Instances(run).Select(async target =>
        {
            await target.Gate.WaitAsync();
            try
            {
                if (target.CleanupComplete) return;
                if (Alive(target))
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        var status = await pipe.SendAsync(target.Identity, "status", new { }, false, timeout.Token);
                        ApplyStatus(target, status);
                        using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await pipe.SendAsync(target.Identity, "shutdown", new { }, true, shutdownTimeout.Token);
                    }
                    catch (Exception exception)
                    {
                        target.Error = new LiveTestError("shutdown_failed", exception.Message, true);
                    }
                }
                if (!target.Stopped)
                {
                    await target.Process.StopAsync(TimeSpan.FromSeconds(3));
                    target.Stopped = !target.Process.IsAlive && !target.Process.IsTreeAlive;
                    if (!target.Stopped) throw new IOException("Owned process or descendants are still alive after stopping.");
                    target.Process.Dispose();
                }
                if (target.LogPath != null)
                {
                    string archive = Path.Combine(run.Directory, target.Name + ".log");
                    File.Copy(target.LogPath, archive, overwrite: true);
                    target.ArchivedLogPath = archive;
                }
                target.CleanupComplete = true;
                target.Status = null;
                target.Error = null;
            }
            catch (Exception exception)
            {
                target.Error = new LiveTestError("cleanup_failed", exception.Message, false);
                run.Error = "One or more instances could not be stopped or archived; inspect instance errors and retry stop_run.";
            }
            finally { target.Gate.Release(); }
        }));
    }

    private async Task RefreshAsync(Run run, Instance target, CancellationToken cancellationToken)
    {
        await target.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!Alive(target)) { target.Status = null; return; }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await pipe.SendAsync(target.Identity, "status", new { }, false, timeout.Token);
            ApplyStatus(target, response);
        }
        finally { target.Gate.Release(); }
    }

    private void ApplyStatus(Instance target, LiveTestResponse response)
    {
        target.Error = response.Error;
        target.Status = response.Ok && response.Result is JsonElement status ? status : null;
        if (target.Status is JsonElement value && value.TryGetProperty("logPath", out var path) &&
            path.ValueKind == JsonValueKind.String && Path.IsPathFullyQualified(path.GetString()))
            target.LogPath = path.GetString();
    }

    private bool Matches(Instance target, string state)
    {
        if (state == "exited") return !Alive(target);
        if (!Alive(target) || target.Status is not JsonElement status || target.Error != null) return false;
        if (state == "controlReady") return true;
        bool Flag(string name) => status.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
        if (state == "readyToJoin") return Flag("readyForClientJoin");
        return Flag(state);
    }

    private async Task<LiveTestResponse> CheckLoadedBridgeAsync(Instance target, Guid expected, CancellationToken cancellationToken)
    {
        var status = await pipe.SendAsync(target.Identity, "status", new { }, false, cancellationToken);
        ApplyStatus(target, status);
        if (!status.Ok) return status;
        if (status.Result is not JsonElement value || !value.TryGetProperty("assemblyMvid", out var mvid) ||
            mvid.ValueKind != JsonValueKind.String || !mvid.TryGetGuid(out var actual) || actual != expected ||
            !value.TryGetProperty("protocolVersion", out var version) || version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int protocol) || protocol != LiveTestProtocol.Version)
            return LocalFailure(target, "bridge_build_mismatch", "Loaded bridge does not match preflight. No join was sent.");
        return null;
    }

    private LiveTestResponse LocalFailure(Instance target, string code, string message) => LiveTestResponse.Failure(
        Guid.NewGuid().ToString("N"), new LiveTestProcessInfo { Pid = target.Identity.Pid, ProcessStartedUtc = target.Identity.StartedUtc,
            Role = target.Identity.Role, PlatformId = target.Identity.PlatformId, RunToken = target.Identity.RunToken }, new LiveTestError(code, message, false));
    private Instance[] Instances(Run run) { lock (run.ArtifactGate) return run.Instances.ToArray(); }
    private bool Alive(Instance target) => !target.Stopped && target.Process.IsAlive;
    private Run FindRun(string id) => runs.TryGetValue(id, out var run) ? run : throw new ArgumentException("Unknown run_id in this MCP session.");
    private Instance FindInstance(Run run, string name)
    {
        var instance = Instances(run).SingleOrDefault(i => i.Name == name);
        if (instance == null) throw new ArgumentException("Unknown instance; use server, client1, client2, etc. from get_run.");
        return instance;
    }
    private bool? TreeAlive(Instance target)
    {
        if (target.Stopped) return false;
        try { return target.Process.IsTreeAlive; }
        catch (Exception e)
        {
            target.Error ??= new LiveTestError("ownership_probe_failed", e.Message, false);
            return null;
        }
    }
    private InstanceView View(Instance i)
    {
        bool? treeAlive = TreeAlive(i);
        return new(i.Name, i.Identity, Alive(i), i.Status, i.Error, treeAlive, i.CleanupComplete, i.StartupPopup);
    }
    private RunView View(Run run) => new(run.Id, run.Profile, run.Directory, run.State, run.Error, Instances(run).Select(View).ToArray(), run.Preflight, new Dictionary<int, string>(run.ClientAttempts), run.RequestedSave, new Dictionary<int, LiveTestError>(run.ClientLaunchErrors));
    private void Save(Run run)
    {
        lock (run.ArtifactGate)
            File.WriteAllText(Path.Combine(run.Directory, "run.json"), JsonSerializer.Serialize(View(run), JsonOptions));
    }
}
