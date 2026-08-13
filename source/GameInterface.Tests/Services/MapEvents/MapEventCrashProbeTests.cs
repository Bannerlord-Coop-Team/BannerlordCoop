using GameInterface.Services.MapEvents.Diagnostics;
using GameInterface.Services.MapEvents.Patches;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Engine;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventCrashProbeTests
{
    [Fact]
    public void CrashInformationKeepsBoundedNewestBoundaries()
    {
        for (int index = 0; index < 40; index++)
            MapEventCrashProbe.Record("boundary-" + index);

        CrashInformationCollector.CrashInformation information = GetCrashInformation();

        Assert.Equal("BannerlordCoop MapEvent crash probe", information.Id);
        Assert.Equal(33, information.Lines.Count);
        Assert.Equal(("Marker", "[MapEventCrashProbe]"), information.Lines[0]);
        Assert.Contains("operation=boundary-39", information.Lines[1].Item2);
        Assert.DoesNotContain(information.Lines, line => line.Item2.Contains("operation=boundary-0"));
    }

    [Fact]
    public void ContextCaptureToleratesMissingGameObjects()
    {
        MapEventCrashProbe.RecordMapEvent("null-map-event", null);
        MapEventCrashProbe.RecordParty("null-party", null, cachedHasMapEvent: true);

        CrashInformationCollector.CrashInformation information = GetCrashInformation();

        Assert.Contains(information.Lines, line => line.Item2.Contains("operation=null-map-event") && line.Item2.Contains("mapEvent=null"));
        Assert.Contains(information.Lines, line => line.Item2.Contains("operation=null-party") && line.Item2.Contains("party=null"));
    }

    [Fact]
    public void DiagnosticFinalizersPreserveManagedException()
    {
        var expected = new InvalidOperationException("probe");

        Exception actual = InvokeFinalizer("FinalizerMapEventManagerTick", expected);

        Assert.Same(expected, actual);
    }

    private static CrashInformationCollector.CrashInformation GetCrashInformation()
    {
        MethodInfo method = typeof(MapEventCrashProbe).GetMethod(
            "GetCrashInformation",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (CrashInformationCollector.CrashInformation)method.Invoke(null, Array.Empty<object>());
    }

    private static Exception InvokeFinalizer(string name, Exception exception)
    {
        MethodInfo method = typeof(MapEventCrashProbePatches).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static);

        return (Exception)method.Invoke(null, new object[] { exception });
    }
}
