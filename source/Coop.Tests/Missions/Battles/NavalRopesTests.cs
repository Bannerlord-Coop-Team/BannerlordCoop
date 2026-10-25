#if COOP_NAVAL
using Missions.Messages;
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System;
using TaleWorlds.Engine;
using Xunit;
using static Missions.Naval.NavalRopes;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class NavalRopesTests
{
    [Theory]
    [InlineData(2, true, 1, BattleRopeState.RopesPulling, ReplicaStep.Ignore)]
    [InlineData(2, false, 1, BattleRopeState.RopeThrown, ReplicaStep.Ignore)]
    [InlineData(2, true, 2, BattleRopeState.RopesPulling, ReplicaStep.Update)]
    [InlineData(2, false, 2, BattleRopeState.RopesPulling, ReplicaStep.Update)]
    [InlineData(2, true, 2, BattleRopeState.Removed, ReplicaStep.Remove)]
    [InlineData(2, false, 2, BattleRopeState.Removed, ReplicaStep.Ignore)]
    [InlineData(2, true, 3, BattleRopeState.RopeThrown, ReplicaStep.Recreate)]
    [InlineData(2, false, 3, BattleRopeState.RopeThrown, ReplicaStep.Update)]
    [InlineData(2, true, 3, BattleRopeState.Removed, ReplicaStep.Remove)]
    public void Decide_OrdersOwnerStatesByGeneration(long replicaGeneration, bool replicaExists, long generation, int state, ReplicaStep expected)
    {
        Assert.Equal(expected, Decide(replicaGeneration, replicaExists, BattleRopeStateTests.Rope(generation, state)));
    }

    [Theory]
    [InlineData(BattleRopeState.RopeThrown, false, true, null)]
    [InlineData(BattleRopeState.RopesPulling, true, true, null)]
    [InlineData(BattleRopeState.RopesPulling, true, false, "target_station_missing")]
    [InlineData(BattleRopeState.BridgeConnected, true, false, "target_station_missing")]
    [InlineData(BattleRopeState.RopesPulling, false, false, "target_required")]
    [InlineData(BattleRopeState.BridgeThrown, false, false, "target_required")]
    [InlineData(BattleRopeState.RopeFailedAndReloading, false, false, null)]
    public void TargetReject_NamesAStateItsTargetCantCarry(int state, bool hasTarget, bool targetResolved, string expected)
    {
        var value = BattleRopeStateTests.Rope(1, state);
        if (!hasTarget)
        {
            value.TargetShipId = Guid.Empty;
            value.TargetKey = null;
        }

        Assert.Equal(expected, TargetReject(value, targetResolved));
    }

    // S17 D1: an expected reject is counted, not thrown.
    [Fact]
    public void Apply_CountsARejectWithItsReason()
    {
        Clear();
        try
        {
            Apply(ShipTestHulls.Uninitialized<MissionShip>(), new[] { BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown) }, _ => null, final: false);

            Assert.Equal(1L, replicaRejects);
            Assert.Equal(0L, replicaFaults);
            Assert.EndsWith(":source_station_missing", lastReplicaReject);
            Assert.False(BypassReplicaGates);
        }
        finally
        {
            Clear();
        }
    }

    // S17 D1: a real fault (here the engine missing behind a resolvable hull) is counted apart from rejects, and the rest still apply.
    [Fact]
    public void Apply_CountsAFaultApartFromRejects_AndAppliesTheRest()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(hull, NavalShipEngineTests.ValidEntity());
        var ropes = new[]
        {
            BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown),
            BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown, "4:attachment_machine_2"),
        };
        Clear();
        try
        {
            Apply(hull, ropes, _ => null, final: false);

            Assert.Equal(2L, replicaFaults);
            Assert.Equal(0L, replicaRejects);
            Assert.Contains(":fault:", lastReplicaReject);
            Assert.StartsWith("4:attachment_machine_2", lastReplicaReject);
        }
        finally
        {
            Clear();
        }
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void AllowsChange_OnlyTheSourceOwnerOrAnAppliedOwnerStateChangesARope(bool sourceIsCopy, bool applying, bool expected)
    {
        Assert.Equal(expected, AllowsChange(sourceIsCopy, applying));
    }

    [Fact]
    public void Removed_KeepsTheStationGenerationAndTarget()
    {
        var pulling = BattleRopeStateTests.Rope(4, BattleRopeState.RopesPulling);

        var removed = Removed(pulling);

        Assert.True(removed.IsValid);
        Assert.Equal(BattleRopeState.Removed, removed.State);
        Assert.Equal(pulling.Generation, removed.Generation);
        Assert.Equal(pulling.SourceKey, removed.SourceKey);
        Assert.Equal(pulling.TargetShipId, removed.TargetShipId);
        Assert.Null(removed.PlankFlight);
    }
}
#endif
