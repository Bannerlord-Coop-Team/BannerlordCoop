using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeWeaponFireReplicatorTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void NetworkGateHit_AppliesDamageOnlyOnTheHostForARemoteRam(
        bool isLocalHost,
        bool ramSimulatedLocally,
        bool expected)
    {
        var method = typeof(SiegeWeaponFireReplicator).GetMethod(
            "ShouldApplyHostGateDamage",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        bool result = Assert.IsType<bool>(method.Invoke(null, new object[] { isLocalHost, ramSimulatedLocally }));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SiegeMachineEvents_AcceptOnlyTheCurrentAuthorityTuple()
    {
        Assert.True(SiegeWeaponFireReplicator.IsCurrentMachineAuthority(
            "owner-b", 7, 3, "owner-b", 7, 3));
        Assert.False(SiegeWeaponFireReplicator.IsCurrentMachineAuthority(
            "owner-a", 7, 2, "owner-b", 7, 3));
        Assert.False(SiegeWeaponFireReplicator.IsCurrentMachineAuthority(
            "owner-b", 6, 3, "owner-b", 7, 3));
    }

    [Fact]
    public void SiegeMachineEvents_ClassifyFutureAndConflictingAuthorityTuples()
    {
        Assert.Equal(1, SiegeWeaponFireReplicator.CompareMachineAuthority(
            "owner-b", 7, 4, "owner-b", 7, 3));
        Assert.Equal(-1, SiegeWeaponFireReplicator.CompareMachineAuthority(
            "owner-a", 7, 2, "owner-b", 7, 3));
        Assert.Equal(2, SiegeWeaponFireReplicator.CompareMachineAuthority(
            "owner-a", 7, 3, "owner-b", 7, 3));
    }

    [Fact]
    public void FutureFire_IsBufferedUntilAuthorityChange_AndStaleOrConflictingFireIsDroppedWithoutLosingCurrentFire()
    {
        const int machineId = 42;
        using var missionScope = new MissionCurrentScope();
        SiegeMissionAuthorityGate.ResetClaimedMachines();

        var broker = new Mock<IMessageBroker>();
        Action<MessagePayload<NetworkSiegeWeaponFired>> receive = null;
        broker.Setup(b => b.Subscribe(
                It.IsAny<Action<MessagePayload<NetworkSiegeWeaponFired>>>()))
            .Callback<Action<MessagePayload<NetworkSiegeWeaponFired>>>(handler => receive = handler);

        var machineState = new Mock<ISiegeMachineStateReplicator>();
        ConfigureAuthority(machineState, "owner-a", 7, 3);
        using var sut = new SiegeWeaponFireReplicator(
            Mock.Of<IBattleNetwork>(),
            broker.Object,
            Mock.Of<INetworkAgentRegistry>(),
            Mock.Of<IBattleSession>(),
            machineState.Object);

        Assert.NotNull(receive);
        var future = Fire(machineId, "owner-a", 7, 4);
        receive(new MessagePayload<NetworkSiegeWeaponFired>(this, future));
        DrainGameThread();
        Assert.Equal(1, PendingFireCount(sut));

        ConfigureAuthority(machineState, "owner-a", 7, 4);
        machineState.Raise(state => state.AuthorityChanged += null, machineId);
        DrainGameThread();
        Assert.Equal(1, PendingFireCount(sut));

        receive(new MessagePayload<NetworkSiegeWeaponFired>(this, Fire(machineId, "owner-a", 7, 3)));
        DrainGameThread();
        receive(new MessagePayload<NetworkSiegeWeaponFired>(this, Fire(machineId, "owner-b", 7, 4)));
        DrainGameThread();
        sut.Tick(0f);
        DrainGameThread();
        Assert.Equal(1, PendingFireCount(sut));
    }

    [Fact]
    public void AuthorityReplay_RetainsFireWhenNativeReplayThrows()
    {
        const int machineId = 43;
        using var missionScope = new MissionCurrentScope();
        SiegeMissionAuthorityGate.ResetClaimedMachines();

        var machineState = new Mock<ISiegeMachineStateReplicator>();
        ConfigureAuthority(machineState, "owner-a", 7, 4);
        using var sut = new SiegeWeaponFireReplicator(
            Mock.Of<IBattleNetwork>(),
            Mock.Of<IMessageBroker>(),
            Mock.Of<INetworkAgentRegistry>(),
            Mock.Of<IBattleSession>(),
            machineState.Object);

        var missionObjectsField = typeof(Mission).GetField(
            "_missionObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(missionObjectsField);
        missionObjectsField!.SetValue(missionScope.Instance, null);

        var replay = typeof(SiegeWeaponFireReplicator).GetMethod(
            "ReplayPendingNetworkFire", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(replay);
        replay!.Invoke(sut, new object[] { Fire(machineId, "owner-a", 7, 4) });

        Assert.Equal(1, PendingFireCount(sut));
    }

    private static NetworkSiegeWeaponFired Fire(
        int machineId,
        string controllerId,
        int hostEpoch,
        int authorityRevision)
        => new(machineId, Guid.Empty, default, default, default, 0f, 0f, "stone",
            controllerId, hostEpoch, authorityRevision);

    private static void ConfigureAuthority(
        Mock<ISiegeMachineStateReplicator> machineState,
        string controllerId,
        int hostEpoch,
        int authorityRevision)
    {
        machineState.Setup(state => state.TryGetMachineAuthority(
                It.IsAny<int>(),
                out controllerId,
                out hostEpoch,
                out authorityRevision))
            .Returns(true);
    }

    private static int PendingFireCount(SiegeWeaponFireReplicator replicator)
    {
        var field = typeof(SiegeWeaponFireReplicator).GetField(
            "pendingNetworkFires", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var pending = Assert.IsType<Dictionary<int, List<NetworkSiegeWeaponFired>>>(field!.GetValue(replicator));
        return pending.Values.Sum(messages => messages.Count);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
