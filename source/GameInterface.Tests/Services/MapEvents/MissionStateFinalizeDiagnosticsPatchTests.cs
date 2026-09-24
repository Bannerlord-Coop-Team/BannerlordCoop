using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEvents;
using Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
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
    public void ExitDiagnostics_RecordRequestAndResultWithoutChangingMission()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        var logs = new List<string>();
        Action<string> capture = logs.Add;
        OutputSinkManager.AddLogCallback(capture);
        BattleSpawnGate.BeginBattle("diagnostic-battle");
        try
        {
            var state = mission.CurrentState;
            MissionStateFinalizeDiagnosticsPatch.EndMissionPrefix(mission);
            MissionStateFinalizeDiagnosticsPatch.MissionResultReadyPrefix(mission, null);
            Assert.False(mission.MissionEnded);
            Assert.Equal(state, mission.CurrentState);
            Assert.Null(mission.MissionResult);
            Assert.Single(logs.Where(log => log.Contains("Mission end requested:") &&
                log.Contains("diagnostic-battle") && log.Contains(nameof(ExitDiagnostics_RecordRequestAndResultWithoutChangingMission))));
            Assert.Single(logs.Where(log => log.Contains("Mission result ready:") &&
                log.Contains("diagnostic-battle")));

            logs.Clear();
            BattleSpawnGate.EndBattle();
            MissionStateFinalizeDiagnosticsPatch.EndMissionPrefix(mission);
            MissionStateFinalizeDiagnosticsPatch.MissionResultReadyPrefix(mission, new MissionResult());
            Assert.Empty(logs.Where(log => log.Contains("[BattleMissionLifecycle]")));
        }
        finally
        {
            BattleSpawnGate.EndBattle();
            OutputSinkManager.RemoveLogCallback(capture);
        }
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
