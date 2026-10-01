#if DEBUG
using Missions.Diagnostics;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Coop.Tests.Missions;

public class MissionRetentionDiagnosticsTests
{
    [Fact]
    public void ObserverDoesNotRetainGraphsAndCountsSameCycleOnce()
    {
        var diagnostics = new MissionRetentionDiagnostics();
        TrackGraph(diagnostics);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var sample = JObject.FromObject(diagnostics.Read(GC.GetTotalMemory(false), fullCollection: true));
        Assert.Equal(1, sample["observedMissionCount"]!.Value<int>());
        Assert.False(sample["cycles"]![0]!["controllerAlive"]!.Value<bool>());
        Assert.All(sample["cycles"]![0]!["types"]!, type => Assert.Equal(0, type["alive"]!.Value<int>()));
        Assert.True(sample["diagnosticFullCollection"]!.Value<bool>());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackGraph(MissionRetentionDiagnostics diagnostics)
    {
        var controller = new object();
        var component = new object();
        diagnostics.Track(controller, new[] { component });
        diagnostics.Track(controller, new[] { component });
        var sample = JObject.FromObject(diagnostics.Read(1));
        Assert.True(sample["cycles"]![0]!["controllerAlive"]!.Value<bool>());
        Assert.Equal(2, sample["cycles"]![0]!["types"]![0]!["observed"]!.Value<int>());
        Assert.False(sample["diagnosticFullCollection"]!.Value<bool>());
        GC.KeepAlive(controller);
        GC.KeepAlive(component);
    }
}
#endif
