using Common.Messaging;
using Missions;
using Missions.Battles;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class SiegeEngineDeploymentReplicatorTests : IDisposable
{
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly SiegeEngineDeploymentReplicator sut;

    public SiegeEngineDeploymentReplicatorTests()
    {
        sut = new SiegeEngineDeploymentReplicator(network.Object, messageBroker.Object, session.Object, new HostEpochPolicy());
    }

    [Fact]
    public void PlacementHistory_PreservesEveryRepeatedPointTransition()
    {
        Invoke("Record", 7, "Ballista");
        Invoke("Record", 7, null);
        Invoke("Record", 7, "FireBallista");

        var history = GetTransitions("placements");

        Assert.Collection(history,
            transition => AssertTransition(transition, 7, "Ballista"),
            transition => AssertTransition(transition, 7, null),
            transition => AssertTransition(transition, 7, "FireBallista"));
    }

    [Fact]
    public void PendingDrain_AppliesInFifoOrder_AndStopsAtFirstBlockedTransition()
    {
        Invoke("StashPending", 1, "Ballista");
        Invoke("StashPending", 2, "Mangonel");
        Invoke("StashPending", 1, null);

        var attempted = new List<int>();
        Func<int, string, bool> blockSecond = (pointId, _) =>
        {
            attempted.Add(pointId);
            return pointId != 2;
        };

        Invoke("DrainPendingInOrder", blockSecond);

        Assert.Equal(new[] { 1, 2 }, attempted);
        Assert.Collection(GetTransitions("pending"),
            transition => AssertTransition(transition, 2, "Mangonel"),
            transition => AssertTransition(transition, 1, null));

        attempted.Clear();
        Invoke("DrainPendingInOrder", new Func<int, string, bool>((pointId, _) =>
        {
            attempted.Add(pointId);
            return true;
        }));

        Assert.Equal(new[] { 2, 1 }, attempted);
        Assert.Empty(GetTransitions("pending"));
    }

    [Fact]
    public void StallDeadline_DropsOnlyUnresolvableHeads_AndAppliesLaterTransitionsInOrder()
    {
        Invoke("StashPending", 1, "MissingOne");
        Invoke("StashPending", 2, "Ballista");
        Invoke("StashPending", 3, "MissingThree");
        Invoke("StashPending", 4, "Mangonel");

        var attempted = new List<int>();
        var applied = new List<int>();
        Func<int, string, bool> applyEvenPoints = (pointId, _) =>
        {
            attempted.Add(pointId);
            if ((pointId & 1) != 0) return false;
            applied.Add(pointId);
            return true;
        };

        Invoke("DropStalledHeadsAndDrain", applyEvenPoints);

        Assert.Equal(new[] { 1, 2, 3, 4 }, attempted);
        Assert.Equal(new[] { 2, 4 }, applied);
        Assert.Empty(GetTransitions("pending"));
    }

    [Fact]
    public void FinishedSuccessorPromotion_RequestsSweep_AndRebroadcastsCompletion()
    {
        int sweeps = 0;
        int broadcasts = 0;

        bool replayed = InvokeStatic<bool>(
            "TryReplayFinishedDeploymentAfterMigration",
            "map-event-1",
            "map-event-1",
            true,
            true,
            true,
            new Action(() => sweeps++),
            new Action(() => broadcasts++));

        Assert.True(replayed);
        Assert.Equal(1, sweeps);
        Assert.Equal(1, broadcasts);
    }

    [Fact]
    public void UnfinishedSuccessorPromotion_DoesNotReplayCompletion()
    {
        int effects = 0;

        bool replayed = InvokeStatic<bool>(
            "TryReplayFinishedDeploymentAfterMigration",
            "map-event-1",
            "map-event-1",
            false,
            true,
            true,
            new Action(() => effects++),
            new Action(() => effects++));

        Assert.False(replayed);
        Assert.Equal(0, effects);
    }

    private void Invoke(string methodName, params object[] arguments)
    {
        var method = typeof(SiegeEngineDeploymentReplicator).GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(sut, arguments);
    }

    private static T InvokeStatic<T>(string methodName, params object[] arguments)
    {
        var method = typeof(SiegeEngineDeploymentReplicator).GetMethod(
            methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<T>(method.Invoke(null, arguments));
    }

    private List<KeyValuePair<int, string>> GetTransitions(string fieldName)
    {
        var field = typeof(SiegeEngineDeploymentReplicator).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<KeyValuePair<int, string>>>(field.GetValue(sut));
    }

    private static void AssertTransition(KeyValuePair<int, string> transition, int pointId, string weaponType)
    {
        Assert.Equal(pointId, transition.Key);
        Assert.Equal(weaponType, transition.Value);
    }

    public void Dispose() => sut.Dispose();
}
