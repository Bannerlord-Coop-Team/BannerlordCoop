using Common.LiveTesting;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CoopMcpServer;

public interface IRunOrchestrator
{
    Task<RunView> StartAsync(string profile, int clientCount, CancellationToken cancellationToken);
    Task<RunView> GetAsync(string runId, CancellationToken cancellationToken);
    Task<object> WaitAsync(string runId, string instance, string state, int timeoutSeconds, CancellationToken cancellationToken);
    Task<LiveTestResponse> RequestAsync(string runId, string instance, string method, object parameters, bool mutation, CancellationToken cancellationToken);
    Task<LogChunk> ReadLogsAsync(string runId, string instance, string cursor, int maxBytes, CancellationToken cancellationToken);
    Task<RunView> StopAsync(string runId);
    Task StopAllAsync();
    string ScreenshotPath(string runId, string instance);
}

public sealed record InstanceView(string Name, InstanceIdentity Identity, bool ProcessAlive, JsonElement? Status, LiveTestError Error);
public sealed record RunView(string RunId, string Profile, string ArtifactDirectory, string State, string Error, InstanceView[] Instances);

public sealed class RunOrchestrator : IRunOrchestrator
{
    private readonly CoopMcpServerSettings settings;
    private readonly IGameProcessLauncher launcher;
    private readonly ILiveTestPipeClient pipe;
    private readonly IIncrementalLogReader logs;
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
    }

    public RunOrchestrator(CoopMcpServerSettings settings, IGameProcessLauncher launcher,
        ILiveTestPipeClient pipe, IIncrementalLogReader logs)
    {
        this.settings = settings;
        this.launcher = launcher;
        this.pipe = pipe;
        this.logs = logs;
    }

    public async Task<RunView> StartAsync(string profile, int clientCount, CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (runs.Values.Any(r => r.State != "stopped" && r.State != "launch_failed"))
                throw new InvalidOperationException("Stop the active run before starting another run.");
            if (!settings.Profiles.TryGetValue(profile, out var launchProfile))
                throw new ArgumentException("Unknown configured profile.");
            launchProfile.Validate(clientCount);
            var run = new Run { Id = Guid.NewGuid().ToString("N"), Profile = profile };
            run.Directory = Path.Combine(settings.ArtifactDirectory, run.Id);
            Directory.CreateDirectory(run.Directory);
            runs[run.Id] = run;
            try
            {
                Save(run);
                for (int index = 0; index <= clientCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string role = index == 0 ? "server" : "client";
                    string platformId = index == 0 ? launchProfile.ServerPlatformId : launchProfile.ClientPlatformIds[index - 1];
                    IOwnedProcess process = launcher.Launch(launchProfile, role, platformId, run.Id);
                    run.Instances.Add(new Instance
                    {
                        Name = index == 0 ? "server" : "client" + index,
                        Process = process,
                        Identity = new InstanceIdentity(process.Pid, process.StartedUtc, role, platformId, run.Id),
                    });
                    Save(run);
                }
            }
            catch (Exception exception)
            {
                run.Error = exception.Message;
                await StopInstancesAsync(run);
                run.State = run.Instances.All(i => i.CleanupComplete) ? "launch_failed" : "cleanup_failed";
                Save(run);
            }
            return View(run);
        }
        finally { lifecycle.Release(); }
    }

    public async Task<RunView> GetAsync(string runId, CancellationToken cancellationToken)
    {
        Run run = FindRun(runId);
        await Task.WhenAll(run.Instances.Select(i => RefreshAsync(run, i, cancellationToken)));
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
        try
        {
            do
            {
                await RefreshAsync(run, target, timeout.Token);
                reached = Matches(target, state);
                if (reached || !Alive(target)) break;
                await Task.Delay(500, timeout.Token);
            } while (true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        return new { reached, state, instance = View(target) };
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
            var response = await pipe.SendAsync(target.Identity, method, parameters, mutation, cancellationToken);
            // Write each result before returning it so uncertain mutations remain inspectable after MCP disconnects.
            try
            {
                File.WriteAllText(Path.Combine(run.Directory, instance + "-" + response.Id + ".json"),
                    LiveTestProtocol.SerializeResponse(response));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return LiveTestResponse.Failure(response.Id, response.Process,
                    new LiveTestError("artifact_write_failed", exception.Message, mutation || (response.Error?.OutcomeUncertain ?? false)));
            }
            return response;
        }
        finally { target.Gate.Release(); }
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

    public async Task<RunView> StopAsync(string runId)
    {
        await lifecycle.WaitAsync();
        try
        {
            var run = FindRun(runId);
            await StopInstancesAsync(run);
            run.State = run.Instances.All(i => i.CleanupComplete) ? "stopped" : "cleanup_failed";
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
            try { await StopAsync(run.Id); }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (failures.Count > 0) throw new AggregateException("One or more runs failed cleanup.", failures);
    }

    private async Task StopInstancesAsync(Run run)
    {
        await Task.WhenAll(run.Instances.Select(async target =>
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
                    target.Stopped = !target.Process.IsAlive;
                    if (!target.Stopped) throw new IOException("Owned process is still alive after stopping.");
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

    private bool Alive(Instance target) => !target.Stopped && target.Process.IsAlive;
    private Run FindRun(string id) => runs.TryGetValue(id, out var run) ? run : throw new ArgumentException("Unknown run_id in this MCP session.");
    private Instance FindInstance(Run run, string name)
    {
        var instance = run.Instances.SingleOrDefault(i => i.Name == name);
        if (instance == null) throw new ArgumentException("Unknown instance; use server, client1, client2, etc. from get_run.");
        return instance;
    }
    private InstanceView View(Instance i) => new(i.Name, i.Identity, Alive(i), i.Status, i.Error);
    private RunView View(Run run) => new(run.Id, run.Profile, run.Directory, run.State, run.Error, run.Instances.Select(View).ToArray());
    private void Save(Run run)
    {
        lock (run.ArtifactGate)
            File.WriteAllText(Path.Combine(run.Directory, "run.json"), JsonSerializer.Serialize(View(run), JsonOptions));
    }
}
