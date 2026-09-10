using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace CoopMcpServer;

public interface IMemoryCommitProbe
{
    CommitSnapshot Read();
}

public sealed class MemoryCommitProbe : IMemoryCommitProbe
{
    public CommitSnapshot Read()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("System commit preflight requires Windows.");
        var info = new PerformanceInformation { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (!GetPerformanceInfo(ref info, info.Size)) throw new Win32Exception();
        return new CommitSnapshot(checked((long)info.CommitTotal * (long)info.PageSize),
            checked((long)info.CommitLimit * (long)info.PageSize));
    }
    [StructLayout(LayoutKind.Sequential)] private struct PerformanceInformation
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable,
            SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [DllImport("psapi.dll", SetLastError = true)] private static extern bool GetPerformanceInfo(ref PerformanceInformation info, uint size);
}

public sealed record CommitSnapshot(long CommittedBytes, long LimitBytes);
#nullable enable annotations
// Required output-schema fields must remain present even when an early failure leaves them unknown.
public sealed record BridgeBuild(string Path, Guid Mvid,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Protocol,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Capabilities);
public sealed record PreflightReport(bool Allowed, string Code, string Message, int NewProcesses,
    long EstimatedBytesPerProcess, long ReserveBytes, long EstimatedRequiredBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] CommitSnapshot? Commit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] BridgeBuild? Bridge);
#nullable restore annotations

public interface IBridgeBuildInspector
{
    BridgeBuild Inspect(LaunchProfile profile);
}

public sealed class BridgeBuildInspector : IBridgeBuildInspector
{
    public BridgeBuild Inspect(LaunchProfile profile)
    {
        string gameRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(profile.Executable), "..", ".."));
        string path = Path.Combine(gameRoot, "Modules", "Coop", "bin", "Win64_Shipping_Client", "Coop.dll");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var markers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != "AssemblyMetadataAttribute") continue;
            var blob = reader.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) continue;
            string key = blob.ReadSerializedString();
            string value = blob.ReadSerializedString();
            if (key != null) markers[key] = value;
        }
        markers.TryGetValue("CoopLiveTestProtocol", out string protocol);
        markers.TryGetValue("CoopLiveTestCapabilities", out string capabilities);
        return new BridgeBuild(path, reader.GetGuid(reader.GetModuleDefinition().Mvid), protocol, capabilities);
    }
}

public interface ILaunchPreflight
{
    PreflightReport Check(LaunchProfile profile, int newProcesses);
}

public sealed class LaunchPreflight : ILaunchPreflight
{
    private readonly IMemoryCommitProbe memory;
    private readonly IBridgeBuildInspector builds;
    public LaunchPreflight(IMemoryCommitProbe memory, IBridgeBuildInspector builds) { this.memory = memory; this.builds = builds; }
    public PreflightReport Check(LaunchProfile profile, int newProcesses)
    {
        if (newProcesses < 0 || newProcesses > 17) throw new ArgumentOutOfRangeException(nameof(newProcesses));
        profile.ValidateBudgets();
        long required = checked((profile.EstimatedCommitBytesPerProcess * newProcesses) + profile.CommitReserveBytes);
        CommitSnapshot commit = null;
        BridgeBuild bridge = null;
        PreflightReport Result(bool allowed, string code, string message) => new(allowed, code, message, newProcesses,
            profile.EstimatedCommitBytesPerProcess, profile.CommitReserveBytes, required, commit, bridge);
        try { commit = memory.Read(); }
        catch (Exception e) { return Result(false, "commit_probe_unavailable", e.Message); }
        if (commit.CommittedBytes < 0 || commit.LimitBytes <= 0 || commit.CommittedBytes > commit.LimitBytes)
            return Result(false, "commit_probe_invalid", "System commit counters are invalid; no launch was attempted.");
        if (commit.LimitBytes - commit.CommittedBytes < required)
            return Result(false, "commit_headroom_insufficient", "Available system commit is below the configured estimate plus reserve. No pagefile, app, or system setting was changed.");
        try { bridge = builds.Inspect(profile); }
        catch (Exception e) { return Result(false, "bridge_build_unavailable", e.Message); }
        if (bridge.Protocol != Common.LiveTesting.LiveTestProtocol.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) || bridge.Capabilities != "staged-ui-capture-v1")
            return Result(false, "bridge_build_incompatible", "The configured Coop.dll lacks the required DEBUG live-test bridge marker. Build/deploy a compatible DEBUG mod separately, with permission; preflight never deploys.");
        return Result(true, "ready", "Read-only estimate, not a memory reservation or guarantee. Existing processes are already included in committed bytes.");
    }
}
