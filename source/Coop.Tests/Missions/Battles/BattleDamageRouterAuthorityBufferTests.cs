using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Messages;
using Missions.Missiles.Handlers;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class BattleDamageRouterAuthorityBufferTests
{
    [Fact]
    public void RemovedSiegeMissile_ClearsAuthorityStampWithoutAnAgentHit()
    {
        Assert.Null(Mission.Current);
        var broker = new Mock<IMessageBroker>();
        var missionComponent = new Mock<ICoopMissionComponent>();
        missionComponent.SetupGet(component => component.AgentRegistry)
            .Returns(Mock.Of<INetworkAgentRegistry>());
        missionComponent.SetupGet(component => component.MissileHandler)
            .Returns(Mock.Of<IMissileHandler>());

        using var sut = new BattleDamageRouter(
            Mock.Of<IBattleNetwork>(),
            broker.Object,
            missionComponent.Object,
            Mock.Of<IBattleSession>(),
            Mock.Of<ISiegeMachineStateReplicator>(),
            Mock.Of<IGuardedHitWindow>(),
            Mock.Of<IAgentNativeMountState>(),
            Mock.Of<IPuppetMountStateRepairer>());

        using var missionScope = new MissionCurrentScope();
        sut.Initialize(missionScope.Instance);

        FieldInfo authorityField = typeof(BattleDamageRouter).GetField(
            "siegeShotAuthorities", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(authorityField);
        var authorityMap = Assert.IsAssignableFrom<IDictionary>(authorityField!.GetValue(sut));
        Type stampType = authorityMap.GetType().GetGenericArguments()[1];
        object authorityStamp = Activator.CreateInstance(stampType);
        Assert.NotNull(authorityStamp);
        authorityMap.Add(17, authorityStamp);
        Assert.Single(authorityMap.Keys);

        FieldInfo removedEvent = typeof(Mission).GetField(
            "OnMissileRemovedEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(removedEvent);
        var callback = Assert.IsType<Action<int>>(removedEvent!.GetValue(missionScope.Instance));
        callback(17);

        Assert.Empty(authorityMap);
    }

    [Fact]
    public void SiegeMissile_AuthorityStampSurvivesMultipleAgentHitsUntilMissileRemoval()
    {
        const int missileIndex = 17;
        Assert.Null(Mission.Current);
        var broker = new Mock<IMessageBroker>();
        Action<MessagePayload<BattlePuppetHit>> receive = null;
        broker.Setup(b => b.Subscribe(
                It.IsAny<Action<MessagePayload<BattlePuppetHit>>>()))
            .Callback<Action<MessagePayload<BattlePuppetHit>>>(handler => receive = handler);

        var machineState = new Mock<ISiegeMachineStateReplicator>();
        ConfigureAuthority(machineState, "owner-a", 7, 3);
        var missionComponent = new Mock<ICoopMissionComponent>();
        missionComponent.SetupGet(component => component.AgentRegistry)
            .Returns(Mock.Of<INetworkAgentRegistry>());
        missionComponent.SetupGet(component => component.MissileHandler)
            .Returns(Mock.Of<IMissileHandler>());

        using var sut = new BattleDamageRouter(
            Mock.Of<IBattleNetwork>(),
            broker.Object,
            missionComponent.Object,
            Mock.Of<IBattleSession>(),
            machineState.Object,
            Mock.Of<IGuardedHitWindow>(),
            Mock.Of<IAgentNativeMountState>(),
            Mock.Of<IPuppetMountStateRepairer>());

        using var missionScope = new MissionCurrentScope();
        FieldInfo missilesField = typeof(Mission).GetField(
            "_missilesDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(missilesField);
        missilesField!.SetValue(missionScope.Instance, Activator.CreateInstance(missilesField.FieldType));
        sut.Initialize(missionScope.Instance);

        FieldInfo authorityField = typeof(BattleDamageRouter).GetField(
            "siegeShotAuthorities", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(authorityField);
        var authorityMap = Assert.IsAssignableFrom<IDictionary>(authorityField!.GetValue(sut));
        Type stampType = authorityMap.GetType().GetGenericArguments()[1];
        object authorityStamp = Activator.CreateInstance(stampType, 42, "owner-a", 7, 3, true);
        Assert.NotNull(authorityStamp);
        authorityMap.Add(missileIndex, authorityStamp);

        Assert.NotNull(receive);
        var hit = new BattlePuppetHit(null, null, MissileBlow(missileIndex), default);
        receive(new MessagePayload<BattlePuppetHit>(this, hit));
        Assert.Single(authorityMap.Keys);
        receive(new MessagePayload<BattlePuppetHit>(this, hit));
        Assert.Single(authorityMap.Keys);

        FieldInfo removedEvent = typeof(Mission).GetField(
            "OnMissileRemovedEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(removedEvent);
        var callback = Assert.IsType<Action<int>>(removedEvent!.GetValue(missionScope.Instance));
        callback(missileIndex);

        Assert.Empty(authorityMap);
    }

    [Fact]
    public void FutureDamage_IsBufferedUntilAuthorityChange_AndStaleOrConflictingDamageIsDropped()
    {
        const int machineId = 42;
        Guid victimId = Guid.NewGuid();
        var broker = new Mock<IMessageBroker>();
        Action<MessagePayload<NetworkApplyBattleDamage>> receive = null;
        broker.Setup(b => b.Subscribe(
                It.IsAny<Action<MessagePayload<NetworkApplyBattleDamage>>>()))
            .Callback<Action<MessagePayload<NetworkApplyBattleDamage>>>(handler => receive = handler);

        var machineState = new Mock<ISiegeMachineStateReplicator>();
        ConfigureAuthority(machineState, "owner-a", 7, 3);

        var agentRegistry = new Mock<INetworkAgentRegistry>();
        var victimInfo = new CoopAgentInfo(
            "victim-owner", "victim-owner", "victim-owner", null, victimId, 0);
        agentRegistry.Setup(registry => registry.TryGetAgentInfo(victimId, out victimInfo))
            .Returns(true);
        var missionComponent = new Mock<ICoopMissionComponent>();
        missionComponent.SetupGet(component => component.AgentRegistry).Returns(agentRegistry.Object);
        missionComponent.SetupGet(component => component.MissileHandler)
            .Returns(Mock.Of<IMissileHandler>());
        var session = new Mock<IBattleSession>();
        session.SetupGet(current => current.OwnControllerId).Returns("victim-owner");

        using var sut = new BattleDamageRouter(
            Mock.Of<IBattleNetwork>(),
            broker.Object,
            missionComponent.Object,
            session.Object,
            machineState.Object,
            Mock.Of<IGuardedHitWindow>(),
            Mock.Of<IAgentNativeMountState>(),
            Mock.Of<IPuppetMountStateRepairer>());

        Assert.NotNull(receive);
        receive(new MessagePayload<NetworkApplyBattleDamage>(
            this, Damage(victimId, machineId, "owner-a", 7, 4)));
        DrainGameThread();
        Assert.Equal(1, PendingDamageCount(sut));

        ConfigureAuthority(machineState, "owner-a", 7, 4);
        machineState.Raise(state => state.AuthorityChanged += null, machineId);
        DrainGameThread();
        Assert.Equal(0, PendingDamageCount(sut));
        int currentTupleLookups = agentRegistry.Invocations.Count(
            invocation => invocation.Method.Name == nameof(INetworkAgentRegistry.TryGetAgentInfo));
        Assert.True(currentTupleLookups > 0);

        receive(new MessagePayload<NetworkApplyBattleDamage>(
            this, Damage(victimId, machineId, "owner-a", 7, 3)));
        DrainGameThread();
        receive(new MessagePayload<NetworkApplyBattleDamage>(
            this, Damage(victimId, machineId, "owner-b", 7, 4)));
        DrainGameThread();
        Assert.Equal(0, PendingDamageCount(sut));
        Assert.Equal(currentTupleLookups, agentRegistry.Invocations.Count(
            invocation => invocation.Method.Name == nameof(INetworkAgentRegistry.TryGetAgentInfo)));
    }

    [Fact]
    public void FutureSiegeDamage_RecordsImpactHintOnlyAfterCurrentAuthorityIsKnown()
    {
        const int machineId = 44;
        const long shotSequence = 9;
        Guid victimId = Guid.NewGuid();
        var broker = new Mock<IMessageBroker>();
        Action<MessagePayload<NetworkApplyBattleDamage>> receive = null;
        broker.Setup(b => b.Subscribe(
                It.IsAny<Action<MessagePayload<NetworkApplyBattleDamage>>>()))
            .Callback<Action<MessagePayload<NetworkApplyBattleDamage>>>(handler => receive = handler);

        var machineState = new Mock<ISiegeMachineStateReplicator>();
        ConfigureAuthority(machineState, "owner-a", 7, 3);
        var agentRegistry = new Mock<INetworkAgentRegistry>();
        var victimInfo = new CoopAgentInfo(
            "victim-owner", "victim-owner", "victim-owner", null, victimId, 0);
        agentRegistry.Setup(registry => registry.TryGetAgentInfo(victimId, out victimInfo))
            .Returns(true);
        var missileHandler = new Mock<IMissileHandler>();
        var missionComponent = new Mock<ICoopMissionComponent>();
        missionComponent.SetupGet(component => component.AgentRegistry).Returns(agentRegistry.Object);
        missionComponent.SetupGet(component => component.MissileHandler).Returns(missileHandler.Object);
        var session = new Mock<IBattleSession>();
        session.SetupGet(current => current.OwnControllerId).Returns("victim-owner");

        using var sut = new BattleDamageRouter(
            Mock.Of<IBattleNetwork>(),
            broker.Object,
            missionComponent.Object,
            session.Object,
            machineState.Object,
            Mock.Of<IGuardedHitWindow>(),
            Mock.Of<IAgentNativeMountState>(),
            Mock.Of<IPuppetMountStateRepairer>());

        var future = Damage(victimId, machineId, "owner-a", 7, 4, true, shotSequence);
        receive(new MessagePayload<NetworkApplyBattleDamage>(this, future));
        DrainGameThread();
        missileHandler.Verify(handler => handler.RecordImpactHint(
            Guid.Empty, shotSequence, victimId, false, It.IsAny<Vec3>()), Times.Never);

        ConfigureAuthority(machineState, "owner-a", 7, 4);
        machineState.Raise(state => state.AuthorityChanged += null, machineId);
        DrainGameThread();
        missileHandler.Verify(handler => handler.RecordImpactHint(
            Guid.Empty, shotSequence, victimId, false, It.IsAny<Vec3>()), Times.Once);

        receive(new MessagePayload<NetworkApplyBattleDamage>(
            this, Damage(victimId, machineId, "owner-a", 7, 3, true, shotSequence + 1)));
        DrainGameThread();
        missileHandler.Verify(handler => handler.RecordImpactHint(
            Guid.Empty, shotSequence + 1, victimId, false, It.IsAny<Vec3>()), Times.Never);
    }

    private static NetworkApplyBattleDamage Damage(
        Guid victimId,
        int machineId,
        string controllerId,
        int hostEpoch,
        int authorityRevision,
        bool missile = false,
        long missileShotSequence = 0)
    {
        Blow blow = default;
        if (missile)
        {
            blow = MissileBlow(42);
        }

        return new NetworkApplyBattleDamage(victimId, Guid.Empty, blow, default,
            machineId: machineId,
            senderControllerId: controllerId,
            hostEpoch: hostEpoch,
            authorityRevision: authorityRevision,
            missileShotSequence: missileShotSequence);
    }

    private static Blow MissileBlow(int missileIndex)
    {
        var blow = new Blow(17);
        object weaponRecord = blow.WeaponRecord;
        FieldInfo isMissileField = typeof(BlowWeaponRecord).GetField(
            "_isMissile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isMissileField);
        isMissileField.SetValue(weaponRecord, true);
        blow.WeaponRecord = (BlowWeaponRecord)weaponRecord;
        blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = missileIndex;
        return blow;
    }

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

    private static int PendingDamageCount(BattleDamageRouter router)
    {
        var field = typeof(BattleDamageRouter).GetField(
            "pendingAuthorityDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var pending = Assert.IsType<Dictionary<int, List<NetworkApplyBattleDamage>>>(field!.GetValue(router));
        return pending.Values.Sum(messages => messages.Count);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
