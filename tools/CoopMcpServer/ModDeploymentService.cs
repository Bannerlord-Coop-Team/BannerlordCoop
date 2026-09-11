using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoopMcpServer;

public interface IModDeploymentService
{
    Task<DeploymentReport> DeployAsync(string solution, string profile, CancellationToken cancellationToken, string configuration = "Release");
}

public sealed class DeploymentReport
{
    [JsonRequired]
    public string DeploymentId { get; set; } = "";
    [JsonRequired]
    public string State { get; set; } = "preparing";
    [JsonRequired]
    public string ArtifactDirectory { get; set; } = "";
    [JsonRequired]
    public string Solution { get; set; } = "";
    [JsonRequired]
    public string Module { get; set; } = "";
    [JsonRequired]
    public string Configuration { get; set; } = "Release";
    [JsonRequired]
    public string Error { get; set; } = "";
    [JsonRequired]
    public List<DeploymentEntry> Files { get; set; } = new();
    [JsonRequired]
    public List<string> CreatedDirectories { get; set; } = new();
    [JsonRequired]
    public List<string> UnresolvedRestoration { get; set; } = new();
}

#nullable enable annotations
public sealed class DeploymentEntry
{
    [JsonRequired]
    public string Source { get; set; }
    [JsonRequired]
    public string Target { get; set; }
    [JsonRequired]
    public string Staged { get; set; }
    [JsonRequired]
    public string Backup { get; set; }
    [JsonRequired]
    public bool OriginallyExisted { get; set; }
    [JsonRequired]
    public FileAttributes OriginalAttributes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonRequired]
    public FileEvidence? Original { get; set; }
    [JsonRequired]
    public FileEvidence Replacement { get; set; }
    [JsonRequired]
    public bool ApplyAttempted { get; set; }
    [JsonRequired]
    public string Restoration { get; set; } = "not_required";
}

#nullable restore annotations

public sealed class ModDeploymentService : IModDeploymentService
{
    private readonly CoopMcpServerSettings settings;
    private readonly IDeploymentRunGuard runs;
    private readonly IDeploymentLease leases;
    private readonly IDeploymentEnvironment environment;
    private readonly IDeploymentPlan plan;
    private readonly IModBuildService builds;
    private readonly IDeploymentFiles files;
    private readonly IBridgeBuildInspector bridge;
    private readonly IDeploymentPaths paths;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ModDeploymentService(CoopMcpServerSettings settings, IDeploymentRunGuard runs, IDeploymentLease leases,
        IDeploymentEnvironment environment, IDeploymentPlan plan, IModBuildService builds, IDeploymentFiles files, IBridgeBuildInspector bridge, IDeploymentPaths paths)
    {
        this.settings = settings; this.runs = runs; this.leases = leases; this.environment = environment;
        this.plan = plan; this.builds = builds; this.files = files; this.bridge = bridge; this.paths = paths;
    }

    public Task<DeploymentReport> DeployAsync(string solution, string profile, CancellationToken cancellationToken, string configuration = "Release") =>
        runs.DeployAsync(() => DeployCoreAsync(solution, profile, cancellationToken, configuration), cancellationToken);

    private async Task<DeploymentReport> DeployCoreAsync(string solution, string profile, CancellationToken cancellationToken, string configuration)
    {
        if (!settings.Profiles.TryGetValue(profile, out var launch)) throw new ArgumentException("Unknown configured profile.");
        var layout = plan.Validate(solution, launch, configuration);
        var config = launch.Deployment;
        environment.RequireIdle();
        long buildSpace = checked(config.BuildSpaceBytes + config.DiskReserveBytes);
        foreach (string path in new[] { layout.Repository, layout.DurableRoot, Path.GetTempPath(),
            Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages") })
            environment.RequireSpace(path, buildSpace);
        using var lease = new TransferableLease(leases.Acquire(launch));
        var report = new DeploymentReport { DeploymentId = Guid.NewGuid().ToString("N"), Solution = layout.Solution, Module = layout.Module, Configuration = layout.Configuration };
        report.ArtifactDirectory = Path.Combine(layout.DurableRoot, report.DeploymentId);
        Directory.CreateDirectory(report.ArtifactDirectory);
        string recovery = Path.Combine(layout.DurableRoot, "recovery-required.json");
        bool mutationStarted = false;
        try
        {
            Save(report);
            await builds.BuildAsync(layout.Repository, config, report.ArtifactDirectory, cancellationToken, layout.Configuration);
            cancellationToken.ThrowIfCancellationRequested();
            var inputs = plan.Collect(layout, launch);
            long replacementBytes = inputs.Sum(i => new FileInfo(i.Source).Length);
            long originalBytes = inputs.Where(i => File.Exists(i.Target)).Sum(i => new FileInfo(i.Target).Length);
            // Include both volumes' needs even when they share a disk; rollback also needs write headroom.
            long required = checked(originalBytes + (replacementBytes * 2) + config.DiskReserveBytes);
            environment.RequireSpace(layout.DurableRoot, required);
            environment.RequireSpace(layout.Module, required);
            foreach (var input in inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Prepare(report, input);
            }
            if (layout.Configuration == "Debug") ValidateBridge(report);
            report.State = "backup_verified";
            Save(report);
            environment.RequireIdle();
            cancellationToken.ThrowIfCancellationRequested();
            // Persist the complete write set before any game file or directory changes.
            WriteDurable(recovery, JsonSerializer.Serialize(new { report.DeploymentId, report.ArtifactDirectory }, JsonOptions));
            mutationStarted = true;
            report.State = "applying";
            Save(report);
            foreach (var entry in report.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                environment.RequireIdle();
                paths.NoLinks(entry.Target);
                VerifyOriginal(entry);
                CreateDirectories(report, Path.GetDirectoryName(entry.Target));
                entry.ApplyAttempted = true;
                Save(report);
                if (entry.OriginallyExisted) File.SetAttributes(entry.Target, FileAttributes.Normal);
                files.Copy(entry.Staged, entry.Target);
                File.SetAttributes(entry.Target, entry.OriginallyExisted ? entry.OriginalAttributes : FileAttributes.Normal);
                Verify(entry.Target, entry.Replacement);
            }
            report.State = "deployed";
            Save(report);
            File.Delete(recovery);
            return report;
        }
        catch (Exception error)
        {
            if (error is BuildCleanupException cleanup) lease.TransferTo(cleanup);
            report.Error = error.ToString();
            if (mutationStarted) Rollback(report);
            report.State = error is BuildCleanupException ? "build_cleanup_required" : report.UnresolvedRestoration.Count > 0 ? "rollback_failed" : mutationStarted ? "rolled_back" : "failed_before_apply";
            try
            {
                Save(report);
                if (mutationStarted && report.UnresolvedRestoration.Count == 0) File.Delete(recovery);
            }
            catch (Exception persistence)
            {
                report.UnresolvedRestoration.Add("Evidence/recovery marker update failed: " + persistence.Message);
                report.State = "rollback_failed";
            }
            return report;
        }
    }

    private sealed class TransferableLease : IDisposable
    {
        private IDisposable lease;
        public TransferableLease(IDisposable lease) { this.lease = lease; }
        public void TransferTo(BuildCleanupException cleanup)
        {
            cleanup.RetainLease(lease);
            lease = null;
        }
        public void Dispose() => lease?.Dispose();
    }

    private void Prepare(DeploymentReport report, DeploymentInput input)
    {
        string relative = Path.GetRelativePath(report.Module, input.Target);
        var entry = new DeploymentEntry { Source = input.Source, Target = input.Target,
            Staged = Path.Combine(report.ArtifactDirectory, "staged", relative), Backup = Path.Combine(report.ArtifactDirectory, "backup", relative),
            OriginallyExisted = File.Exists(input.Target), Replacement = files.Inspect(input.Source) };
        Directory.CreateDirectory(Path.GetDirectoryName(entry.Staged));
        files.Copy(input.Source, entry.Staged);
        File.SetAttributes(entry.Staged, FileAttributes.Normal);
        Verify(entry.Staged, entry.Replacement);
        if (entry.OriginallyExisted)
        {
            entry.OriginalAttributes = File.GetAttributes(entry.Target);
            const FileAttributes supported = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive |
                FileAttributes.Normal | FileAttributes.NotContentIndexed | FileAttributes.Temporary;
            if ((entry.OriginalAttributes & ~supported) != 0)
                throw new IOException("Cannot exactly restore special file attributes: " + entry.Target);
            entry.Original = files.Inspect(entry.Target);
            Directory.CreateDirectory(Path.GetDirectoryName(entry.Backup));
            files.Copy(entry.Target, entry.Backup);
            File.SetAttributes(entry.Backup, FileAttributes.Normal);
            Verify(entry.Backup, entry.Original);
            VerifyOriginal(entry);
        }
        report.Files.Add(entry);
    }

    private void ValidateBridge(DeploymentReport report)
    {
        // Inspect the staged Coop.dll without loading it or weakening launch preflight.
        string stagedCoop = report.Files.Single(e => Path.GetFileName(e.Target) == "Coop.dll").Staged;
        var inspected = bridge.InspectPath(stagedCoop);
        if (inspected.Protocol != Common.LiveTesting.LiveTestProtocol.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            inspected.Capabilities != "staged-ui-capture-v1") throw new InvalidDataException("Built Coop.dll is not a compatible DEBUG live-test bridge.");
    }

    private void VerifyOriginal(DeploymentEntry entry)
    {
        if (Directory.Exists(entry.Target) || File.Exists(entry.Target) != entry.OriginallyExisted)
            throw new IOException("Target existence changed after backup: " + entry.Target);
        if (!entry.OriginallyExisted) return;
        Verify(entry.Target, entry.Original);
        if (File.GetAttributes(entry.Target) != entry.OriginalAttributes) throw new IOException("Target attributes changed after backup: " + entry.Target);
    }

    private void Verify(string path, FileEvidence expected)
    {
        if (files.Inspect(path) != expected) throw new IOException("File verification failed: " + path);
    }

    private void CreateDirectories(DeploymentReport report, string directory)
    {
        if (Directory.Exists(directory)) return;
        CreateDirectories(report, Path.GetDirectoryName(directory));
        report.CreatedDirectories.Add(directory);
        Save(report);
        Directory.CreateDirectory(directory);
    }

    private void Rollback(DeploymentReport report)
    {
        foreach (var entry in report.Files.Where(e => e.ApplyAttempted).Reverse())
        {
            try
            {
                paths.NoLinks(entry.Target);
                if (entry.OriginallyExisted)
                {
                    Verify(entry.Backup, entry.Original);
                    if (File.Exists(entry.Target)) File.SetAttributes(entry.Target, FileAttributes.Normal);
                    files.Copy(entry.Backup, entry.Target);
                    File.SetAttributes(entry.Target, entry.OriginalAttributes);
                    VerifyOriginal(entry);
                }
                else
                {
                    if (File.Exists(entry.Target)) { File.SetAttributes(entry.Target, FileAttributes.Normal); File.Delete(entry.Target); }
                    if (File.Exists(entry.Target) || Directory.Exists(entry.Target)) throw new IOException("New target still exists.");
                }
                entry.Restoration = "verified";
            }
            catch (Exception error)
            {
                entry.Restoration = "failed: " + error.Message;
                report.UnresolvedRestoration.Add(entry.Target + ": " + error.Message);
            }
        }
        foreach (string directory in report.CreatedDirectories.AsEnumerable().Reverse())
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: false); }
            catch (Exception error) { report.UnresolvedRestoration.Add(directory + ": " + error.Message); }
        }
    }

    private static void Save(DeploymentReport report) => WriteDurable(Path.Combine(report.ArtifactDirectory, "manifest.json"), JsonSerializer.Serialize(report, JsonOptions));

    private static void WriteDurable(string path, string content)
    {
        string pending = path + ".pending";
        using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.Move(pending, path, overwrite: true);
    }
}
