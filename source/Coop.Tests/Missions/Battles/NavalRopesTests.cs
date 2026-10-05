using Missions.Messages;
using Missions.Naval;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;
using static Missions.Naval.NavalRopes;

namespace Coop.Tests.Missions.Battles;

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
        Assert.Equal(expected, Decide(replicaGeneration, replicaExists, Rope(generation, state)));
    }

    [Fact]
    public void Decide_ReplaysAThrowPullRemoveRethrowLifecycleOnce()
    {
        var replica = new ReplicaModel();

        replica.Apply(Rope(1, BattleRopeState.RopeThrown));
        replica.Apply(Rope(1, BattleRopeState.RopesPulling));
        replica.Apply(Rope(1, BattleRopeState.Removed));
        replica.Apply(Rope(1, BattleRopeState.Removed));
        replica.Apply(Rope(2, BattleRopeState.RopeThrown));

        Assert.Equal(new[] { "create:1", "update:1", "remove:1", "create:2" }, replica.Log);
    }

    [Fact]
    public void Decide_RecreatesWhenTheOwnerReplacedARopeBetweenTwoSamples()
    {
        var replica = new ReplicaModel();

        replica.Apply(Rope(1, BattleRopeState.RopesPulling));
        replica.Apply(Rope(2, BattleRopeState.RopeThrown));
        replica.Apply(Rope(1, BattleRopeState.Removed));

        Assert.Equal(new[] { "create:1", "recreate:2" }, replica.Log);
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

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AllowsForce_WritesJointForceOnlyToSimulatedHulls(bool bodyIsCopy, bool expected)
    {
        Assert.Equal(expected, AllowsForce(bodyIsCopy));
    }

    [Fact]
    public void Removed_KeepsTheStationGenerationAndTarget()
    {
        var pulling = Rope(4, BattleRopeState.RopesPulling);

        var removed = Removed(pulling);

        Assert.True(removed.IsValid);
        Assert.Equal(BattleRopeState.Removed, removed.State);
        Assert.Equal(pulling.Generation, removed.Generation);
        Assert.Equal(pulling.SourceKey, removed.SourceKey);
        Assert.Equal(pulling.TargetShipId, removed.TargetShipId);
        Assert.Null(removed.PlankFlight);
    }

    internal static BattleRopeState Rope(long generation, int state, string sourceKey = "3:attachment_machine_1") => new BattleRopeState
    {
        SourceKey = sourceKey,
        Generation = generation,
        State = state,
        TargetShipId = state == BattleRopeState.RopeThrown ? Guid.Empty : new Guid("6f1f0a59-8fb5-4b52-9d36-1d1c9a4f0c11"),
        TargetKey = state == BattleRopeState.RopeThrown ? null : "5:attachment_point_2",
        Length = 12f,
        HookFrame = NetworkBattleShipSample.FromFrame(MatrixFrame.Identity),
        PlankFlight = state == BattleRopeState.BridgeThrown || state == BattleRopeState.BridgeConnected
            ? new[] { 0.5f, 0f, 0.25f, 1f, 0.5f, 0f, 0f, 1f }
            : null,
        DecorationPlanks = state == BattleRopeState.BridgeConnected ? 12 : 0,
    };

    // The replica's attachment lifecycle as ApplyOne drives it from Decide.
    private sealed class ReplicaModel
    {
        private long generation;
        private bool exists;

        public List<string> Log { get; } = new List<string>();

        public void Apply(BattleRopeState value)
        {
            var step = Decide(generation, exists, value);
            switch (step)
            {
                case ReplicaStep.Ignore:
                    return;
                case ReplicaStep.Remove:
                    exists = false;
                    Log.Add("remove:" + generation);
                    return;
                case ReplicaStep.Recreate:
                    generation = value.Generation;
                    Log.Add("recreate:" + generation);
                    return;
            }

            bool created = !exists || value.Generation != generation;
            generation = value.Generation;
            exists = true;
            Log.Add((created ? "create:" : "update:") + generation);
        }
    }
}
