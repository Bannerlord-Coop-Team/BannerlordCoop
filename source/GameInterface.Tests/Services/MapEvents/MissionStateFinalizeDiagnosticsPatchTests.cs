using GameInterface.Services.MapEvents.Patches;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Verifies mission lifecycle identifiers survive until finalization.</summary>
public class MissionStateFinalizeDiagnosticsPatchTests
{
    [Fact]
    public void RecordCorrelation_PreservesSequenceAndMapEventForMissionFinalization()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

        MissionStateFinalizeDiagnosticsPatch.RecordCorrelation(mission, 42, "map-event-7");

        bool found = MissionStateFinalizeDiagnosticsPatch.TryGetCorrelation(
            mission,
            out var sequence,
            out var mapEventId);

        Assert.True(found);
        Assert.Equal(42, sequence);
        Assert.Equal("map-event-7", mapEventId);
    }

    [Fact]
    public void TryGetCorrelation_UntrackedMission_ReturnsNoIdentifiers()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

        bool found = MissionStateFinalizeDiagnosticsPatch.TryGetCorrelation(
            mission,
            out var sequence,
            out var mapEventId);

        Assert.False(found);
        Assert.Equal(0, sequence);
        Assert.Null(mapEventId);
    }
}
