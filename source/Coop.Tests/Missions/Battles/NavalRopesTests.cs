#if COOP_NAVAL
using Missions.Messages;
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System;
using TaleWorlds.Engine;
using Xunit;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using RopeAttachmentState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;
using ShipAttachmentMachine = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine;
using static Missions.Naval.NavalRopeRegistry;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class NavalRopesTests
{
    private readonly NavalRopeRegistry registry = new NavalRopeRegistry(new NavalForeignHullRegistry(), new NavalMissionTracker());

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

    // S17 F1: a removal repeats for the window, not for the rest of the battle.
    [Fact]
    public void Capture_RepeatsAStationsRemovalOnlyForTheRepeatWindow()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var station = ShipTestHulls.Uninitialized<NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine>();
        station.OwnerShip = hull;
        hull._attachmentMachines = new TaleWorlds.Library.MBList<NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine> { station };
        registry.Clear();
        try
        {
            registry.records[station] = new RopeRecord { Generation = 3, Last = BattleRopeStateTests.Rope(3, BattleRopeState.RopesPulling) };

            for (int sample = 0; sample < RemovedRepeatSamples; sample++)
            {
                var removal = Assert.Single(registry.Capture(hull, _ => Guid.Empty));
                Assert.Equal(BattleRopeState.Removed, removal.State);
                Assert.Equal(3L, removal.Generation);
            }

            Assert.Null(registry.records[station].SamplesOmittedUtc);
            Assert.NotNull(registry.records[station].LastIncludedSampleUtc);
            Assert.Null(registry.Capture(hull, _ => Guid.Empty));
            var omittedUtc = registry.records[station].SamplesOmittedUtc;
            Assert.NotNull(omittedUtc);
            Assert.True(omittedUtc >= registry.records[station].LastIncludedSampleUtc);
            Assert.Null(registry.Capture(hull, _ => Guid.Empty));
            Assert.Equal(omittedUtc, registry.records[station].SamplesOmittedUtc);
        }
        finally
        {
            registry.Clear();
        }
    }

#if DEBUG
    /// <summary>The debug hold preserves the live entry for the repeat window without delaying omission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capture_HeldRemovalExpiresByOmissionOrCanBeReleased(bool releaseEarly)
    {
        var (hull, station, _) = ReplicaOnCopy();
        station.CurrentAttachment = null;
        registry.Clear();
        try
        {
            var live = BattleRopeStateTests.Rope(3, BattleRopeState.RopesPulling);
            registry.records[station] = new RopeRecord { Generation = 3, Last = live };
            Assert.True(registry.HoldRemoval(station));
            Assert.False(registry.HoldRemoval(station));
            Assert.NotNull(registry.records[station].RemovalHoldStartedUtc);

            Assert.Same(live, Assert.Single(registry.Capture(hull, _ => Guid.Empty)));
            Assert.Equal(BattleRopeState.Removed, registry.records[station].Last.State);
            if (releaseEarly) Assert.True(registry.ReleaseRemoval(station));

            for (int sample = 1; sample < RemovedRepeatSamples; sample++)
            {
                var sent = Assert.Single(registry.Capture(hull, _ => Guid.Empty));
                Assert.Equal(releaseEarly ? BattleRopeState.Removed : BattleRopeState.RopesPulling, sent.State);
                Assert.Equal(3, sent.Generation);
            }

            Assert.Null(registry.Capture(hull, _ => Guid.Empty));
            Assert.Equal(RemovedRepeatSamples, registry.records[station].RemovedRepeats);
            Assert.NotNull(registry.records[station].SamplesOmittedUtc);
            if (!releaseEarly) Assert.True(registry.ReleaseRemoval(station));
            Assert.NotNull(registry.records[station].RemovalHoldReleasedUtc);
            Assert.False(registry.ReleaseRemoval(station));
            Assert.Null(registry.Capture(hull, _ => Guid.Empty));
            registry.Clear();
            Assert.Null(registry.RecordOf(station));
            Assert.False(registry.ReleaseRemoval(station));
        }
        finally
        {
            registry.Clear();
        }
    }

    /// <summary>A hold cannot arm for an unpublished, stale or already removed generation.</summary>
    [Theory]
    [InlineData(2, BattleRopeState.RopesPulling)]
    [InlineData(3, BattleRopeState.Removed)]
    [InlineData(3, BattleRopeState.RopeThrown)]
    public void HoldRemoval_RejectsUnusablePublishedStates(long generation, int state)
    {
        var (_, station, _) = ReplicaOnCopy();
        registry.Clear();
        try
        {
            Assert.False(registry.HoldRemoval(station));
            registry.records[station] = new RopeRecord { Generation = 3, Last = BattleRopeStateTests.Rope(generation, state) };
            Assert.False(registry.HoldRemoval(station));
            Assert.Null(registry.records[station].HeldRemovalLiveState);
        }
        finally
        {
            registry.Clear();
        }
    }
#endif

    // S17 re-review R1: a peer that missed the whole removal window still drops the replica once a sample omits its station.
    [Fact]
    public void Apply_RemovesAReplicaTheOwnersSampleOmits()
    {
        var (copy, station, rope) = ReplicaOnCopy();
        registry.Clear();
        try
        {
            registry.records[station] = new RopeRecord { Generation = 3, Last = BattleRopeStateTests.Rope(3, BattleRopeState.RopesPulling) };

            registry.Apply(copy, null, _ => null, final: false);

            Assert.Equal(BattleRopeState.Removed, registry.records[station].Last.State);
            Assert.Equal(3L, registry.records[station].Last.Generation);
            // Vanilla's SetAttachmentState sets the state before it reaches the engine, which these tests lack.
            Assert.Equal(RopeAttachmentState.BrokenAndWaitingForRemoval, rope.State);
        }
        finally
        {
            registry.Clear();
        }
    }

    [Fact]
    public void Apply_KeepsAReplicaTheOwnersSampleCarries()
    {
        var (copy, station, rope) = ReplicaOnCopy();
        registry.Clear();
        try
        {
            registry.records[station] = new RopeRecord { Generation = 3, Last = BattleRopeStateTests.Rope(3, BattleRopeState.RopesPulling) };

            registry.Apply(copy, new[] { BattleRopeStateTests.Rope(3, BattleRopeState.RopesPulling) }, _ => null, final: false);
            registry.Apply(copy, null, _ => null, final: true);

            Assert.Equal(BattleRopeState.RopesPulling, registry.records[station].Last.State);
            Assert.Equal(RopeAttachmentState.RopesPulling, rope.State);
        }
        finally
        {
            registry.Clear();
        }
    }

    // A constructor-skipped copy hull whose one station holds a pulling replica rope.
    private static (MissionShip copy, ShipAttachmentMachine station, Attachment rope) ReplicaOnCopy()
    {
        var copy = ShipTestHulls.Uninitialized<MissionShip>();
        var station = ShipTestHulls.Uninitialized<ShipAttachmentMachine>();
        station.OwnerShip = copy;
        copy._attachmentMachines = new TaleWorlds.Library.MBList<ShipAttachmentMachine> { station };
        var rope = (Attachment)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Attachment));
        rope._state = RopeAttachmentState.RopesPulling;
        rope.AttachmentSource = station;
        station.CurrentAttachment = rope;
        return (copy, station, rope);
    }

    // S17 D1: an expected reject is counted, not thrown.
    [Fact]
    public void Apply_CountsARejectWithItsReason()
    {
        registry.Clear();
        try
        {
            var copy = ShipTestHulls.Uninitialized<MissionShip>();
            copy._attachmentMachines = new TaleWorlds.Library.MBList<ShipAttachmentMachine>();

            registry.Apply(copy, new[] { BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown) }, _ => null, final: false);

            Assert.Equal(1L, registry.replicaRejects);
            Assert.Equal(0L, registry.replicaFaults);
            Assert.EndsWith(":source_station_missing", registry.lastReplicaReject);
            Assert.False(registry.BypassReplicaGates);
        }
        finally
        {
            registry.Clear();
        }
    }

    // S17 D1: a real fault (here the engine missing behind a resolvable hull) is counted apart from rejects, and the rest still apply.
    [Fact]
    public void Apply_CountsAFaultApartFromRejects_AndAppliesTheRest()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        hull._attachmentMachines = new TaleWorlds.Library.MBList<ShipAttachmentMachine>();
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(hull, NavalShipEngineTests.ValidEntity());
        var ropes = new[]
        {
            BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown),
            BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown, "4:attachment_machine_2"),
        };
        registry.Clear();
        try
        {
            registry.Apply(hull, ropes, _ => null, final: false);

            Assert.Equal(2L, registry.replicaFaults);
            Assert.Equal(0L, registry.replicaRejects);
            Assert.Contains(":fault:", registry.lastReplicaReject);
            Assert.StartsWith("4:attachment_machine_2", registry.lastReplicaReject);
        }
        finally
        {
            registry.Clear();
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
