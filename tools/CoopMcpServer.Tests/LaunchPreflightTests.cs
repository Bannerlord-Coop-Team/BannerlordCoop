using System.Reflection;

[assembly: AssemblyMetadata("CoopLiveTestProtocol", "1")]
[assembly: AssemblyMetadata("CoopLiveTestCapabilities", "staged-ui-capture-v1")]

namespace CoopMcpServer.Tests;

public sealed class LaunchPreflightTests
{
    private sealed class Probe(long used, long limit) : IMemoryCommitProbe
    {
        public CommitSnapshot Read() => new(used, limit);
    }
    private sealed class UnavailableProbe : IMemoryCommitProbe
    {
        public CommitSnapshot Read() => throw new PlatformNotSupportedException("unavailable");
    }
    private sealed class Builds(string protocol = "1", string capability = "staged-ui-capture-v1") : IBridgeBuildInspector
    {
        public int Calls;
        public BridgeBuild InspectPath(string path) => new(path, Guid.Empty, protocol, capability);
        public BridgeBuild Inspect(LaunchProfile profile) { Calls++; return new("fake", Guid.Empty, protocol, capability); }
    }

    [Theory]
    [InlineData(2, 20, true)]
    [InlineData(2, 19, false)]
    [InlineData(1, 12, true)]
    [InlineData(1, 11, false)]
    [InlineData(0, 4, true)]
    [InlineData(0, 3, false)]
    public void ThresholdAccountsNewProcessesAndReserveAgainstSystemCommit(int count, long limit, bool allowed)
    {
        var builds = new Builds();
        var profile = new LaunchProfile { EstimatedCommitBytesPerProcess = 8, CommitReserveBytes = 2 };
        var result = new LaunchPreflight(new Probe(2, limit), builds).Check(profile, count);
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal((count * 8) + 2, result.EstimatedRequiredBytes);
        Assert.Equal(allowed ? "ready" : "commit_headroom_insufficient", result.Code);
        Assert.Equal(allowed ? 1 : 0, builds.Calls);
    }

    [Fact]
    public void UnknownProbeFailsClosedWithoutGuessingFromPhysicalRam()
    {
        var result = new LaunchPreflight(new UnavailableProbe(), new Builds()).Check(new LaunchProfile(), 1);
        Assert.False(result.Allowed);
        Assert.Equal("commit_probe_unavailable", result.Code);
        Assert.Null(result.Commit);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2", "staged-ui-capture-v1")]
    [InlineData("1", "legacy")]
    public void ReleaseLegacyOrDifferentBridgeIsRejected(string protocol, string capability)
    {
        var result = new LaunchPreflight(new Probe(0, long.MaxValue), new Builds(protocol, capability)).Check(new LaunchProfile(), 1);
        Assert.False(result.Allowed);
        Assert.Equal("bridge_build_incompatible", result.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void InvalidBudgetCannotDisableOrOverflowGuard(long bytes) =>
        Assert.Throws<ArgumentException>(() => new LaunchPreflight(new Probe(0, long.MaxValue), new Builds()).Check(
            new LaunchProfile { EstimatedCommitBytesPerProcess = bytes }, 17));

    [Fact]
    public void MetadataInspectorReadsMarkerAndMvidWithoutLoadingAssembly()
    {
        string root = Path.Combine(Path.GetTempPath(), "CoopMetadata-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "Modules", "Coop", "bin", "Win64_Shipping_Client", "Coop.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        try
        {
            File.Copy(typeof(LaunchPreflightTests).Assembly.Location, target);
            var build = new BridgeBuildInspector().Inspect(new LaunchProfile { Executable = Path.Combine(root, "bin", "Win64_Shipping_Client", "Bannerlord.exe") });
            Assert.Equal("1", build.Protocol);
            Assert.Equal("staged-ui-capture-v1", build.Capabilities);
            Assert.Equal(typeof(LaunchPreflightTests).Module.ModuleVersionId, build.Mvid);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void WindowsProbeReportsCommitRatherThanPhysicalAvailability()
    {
        var result = new MemoryCommitProbe().Read();
        Assert.True(result.CommittedBytes > 0);
        Assert.True(result.LimitBytes >= result.CommittedBytes);
    }
}
