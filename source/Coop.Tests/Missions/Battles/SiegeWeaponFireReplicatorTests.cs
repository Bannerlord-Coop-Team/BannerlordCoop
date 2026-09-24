using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Engine;
using TaleWorlds.Library;
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
        bool result = SiegeWeaponFireReplicator.ShouldApplyHostGateDamage(isLocalHost, ramSimulatedLocally);

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
            machineState.Object, Mock.Of<ISiegeGateHitApplier>(), new HostEpochPolicy());

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
    public void FireBeforeFirstAuthorityAssignment_RemainsBufferedUntilAssignmentAndDropsAfterHandback()
    {
        const int machineId = 42;
        using var missionScope = new MissionCurrentScope();
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        var broker = new Mock<IMessageBroker>();
        Action<MessagePayload<NetworkSiegeWeaponFired>> receive = null;
        broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkSiegeWeaponFired>>>()))
            .Callback<Action<MessagePayload<NetworkSiegeWeaponFired>>>(handler => receive = handler);
        var machineState = new Mock<ISiegeMachineStateReplicator>();
        Assert.False(machineState.Object.TryGetMachineAuthority(machineId, out _, out _, out _));
        using var sut = new SiegeWeaponFireReplicator(Mock.Of<IBattleNetwork>(), broker.Object,
            Mock.Of<INetworkAgentRegistry>(), Mock.Of<IBattleSession>(), machineState.Object,
            Mock.Of<ISiegeGateHitApplier>(), new HostEpochPolicy());

        Assert.NotNull(receive);
        receive(new MessagePayload<NetworkSiegeWeaponFired>(this, Fire(machineId, "owner-a", 7, 3)));
        DrainGameThread();
        Assert.Equal(1, PendingFireCount(sut));
        sut.Tick(0f);
        DrainGameThread();
        Assert.Equal(1, PendingFireCount(sut));

        ConfigureAuthority(machineState, "owner-a", 7, 3);
        machineState.Raise(state => state.AuthorityChanged += null, machineId);
        DrainGameThread();
        // Registration is still absent, so the now-current fire must remain queued.
        Assert.Equal(1, PendingFireCount(sut));

        ConfigureAuthority(machineState, "owner-b", 7, 4);
        machineState.Raise(state => state.AuthorityChanged += null, machineId);
        DrainGameThread();
        Assert.Equal(0, PendingFireCount(sut));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SharedHostEpoch_DropsOldFireAndGateHitsDespiteMatchingMachineAuthority(
        bool gateHit, bool bufferedBeforeMigration)
    {
        using var mission = new MissionCurrentScope();
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        try
        {
            var broker = new Mock<IMessageBroker>();
            Action<MessagePayload<NetworkSiegeWeaponFired>> receiveFire = null;
            Action<MessagePayload<NetworkGateHit>> receiveGateHit = null;
            broker.Setup(x => x.Subscribe(It.IsAny<Action<MessagePayload<NetworkSiegeWeaponFired>>>()))
                .Callback<Action<MessagePayload<NetworkSiegeWeaponFired>>>(handler => receiveFire = handler);
            broker.Setup(x => x.Subscribe(It.IsAny<Action<MessagePayload<NetworkGateHit>>>()))
                .Callback<Action<MessagePayload<NetworkGateHit>>>(handler => receiveGateHit = handler);
            var state = new Mock<ISiegeMachineStateReplicator>();
            ConfigureAuthority(state, "remote", 7, 3);
            var session = new Mock<IBattleSession>();
            session.SetupGet(x => x.HostEpoch).Returns(7);
            session.SetupGet(x => x.IsLocalHost).Returns(true);
            var applier = new Mock<ISiegeGateHitApplier>();
            var policy = new HostEpochPolicy();
            using var sut = new SiegeWeaponFireReplicator(Mock.Of<IBattleNetwork>(), broker.Object,
                Mock.Of<INetworkAgentRegistry>(), session.Object, state.Object, applier.Object, policy);
            void Receive(int epoch)
            {
                if (gateHit)
                    receiveGateHit(new MessagePayload<NetworkGateHit>(this,
                        new NetworkGateHit(44, 42, 250, "remote", epoch, 3)));
                else
                    receiveFire(new MessagePayload<NetworkSiegeWeaponFired>(this, Fire(42, "remote", epoch, 3)));
                DrainGameThread();
            }

            if (bufferedBeforeMigration)
            {
                Receive(7);
                Assert.Equal(1, PendingFireCount(sut) + PendingGateHitCount(sut));
            }
            // Another siege receiver accepts the new epoch before this machine's tuple catches up.
            Assert.False(policy.IsStale(8, session.Object.HostEpoch));
            applier.Invocations.Clear();
            sut.Tick(0f);
            DrainGameThread();
            Receive(7);
            Assert.Equal(0, PendingFireCount(sut) + PendingGateHitCount(sut));
            applier.Verify(x => x.TryApply(It.IsAny<NetworkGateHit>(), It.IsAny<bool>()), Times.Never);

            ConfigureAuthority(state, "remote", 8, 3);
            Receive(8);
            Assert.Equal(1, PendingFireCount(sut) + PendingGateHitCount(sut));
        }
        finally
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }
    }

    private static Func<RangedSiegeWeapon, NetworkSiegeWeaponFired, bool> projectileReplay;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BufferedFire_AppliesOnceAfterRegistration_AndNeverRetriesReplayFailure(bool throwsAfterReplay)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.buffered-siege-fire");
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeWeaponFireReplicatorTests), nameof(SkipScriptComponentCache))));
#pragma warning disable SYSLIB0050
            var weapon = (Ballista)FormatterServices.GetUninitializedObject(typeof(Ballista));
#pragma warning restore SYSLIB0050
            int machineId = weapon.Id.Id;
            int replays = 0;
            projectileReplay = (actualWeapon, message) =>
            {
                Assert.Same(weapon, actualWeapon);
                Assert.Equal(machineId, message.MachineId);
                replays++;
                if (throwsAfterReplay) throw new InvalidOperationException("after projectile replay");
                return true;
            };
            harmony.Patch(AccessTools.Method(typeof(SiegeWeaponFireReplicator), "TrySpawnProjectile"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeWeaponFireReplicatorTests), nameof(ReplayProjectile))));
            var broker = new Mock<IMessageBroker>();
            Action<MessagePayload<NetworkSiegeWeaponFired>> receive = null;
            broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkSiegeWeaponFired>>>()))
                .Callback<Action<MessagePayload<NetworkSiegeWeaponFired>>>(handler => receive = handler);
            var state = new Mock<ISiegeMachineStateReplicator>();
            ConfigureAuthority(state, "remote", 7, 3);
            using var sut = new SiegeWeaponFireReplicator(Mock.Of<IBattleNetwork>(), broker.Object,
                Mock.Of<INetworkAgentRegistry>(), Mock.Of<IBattleSession>(), state.Object,
                Mock.Of<ISiegeGateHitApplier>(), new HostEpochPolicy());

            receive(new MessagePayload<NetworkSiegeWeaponFired>(this, Fire(machineId, "remote", 7, 4)));
            DrainGameThread();
            Assert.Equal(1, PendingFireCount(sut));
            ConfigureAuthority(state, "remote", 7, 4);
            state.Raise(x => x.AuthorityChanged += null, machineId);
            DrainGameThread();
            sut.Tick(0f);
            DrainGameThread();
            Assert.Equal(0, replays);
            Assert.Equal(1, PendingFireCount(sut));

            var objects = (MBList<MissionObject>)AccessTools.Field(typeof(Mission), "_missionObjects").GetValue(mission.Instance);
            objects.Add(weapon);
            sut.Tick(0f);
            DrainGameThread();
            Assert.Equal(1, replays);
            Assert.Equal(0, PendingFireCount(sut));
            sut.Tick(0f);
            state.Raise(x => x.AuthorityChanged += null, machineId);
            DrainGameThread();
            Assert.Equal(1, replays);
            Assert.Equal(0, PendingFireCount(sut));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            projectileReplay = null;
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }
    }

    private static bool SkipScriptComponentCache() => false;

    private static bool ReplayProjectile(RangedSiegeWeapon weapon, NetworkSiegeWeaponFired msg, ref bool __result)
    {
        __result = projectileReplay(weapon, msg);
        return false;
    }

    [Fact]
    public void FutureGateHit_WaitsForAuthorityAndObjectRegistration_AndDropsAfterHandback()
    {
        const int ramId = 43;
        using var missionScope = new MissionCurrentScope();
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        var broker = new Mock<IMessageBroker>();
        Action<MessagePayload<NetworkGateHit>> receive = null;
        broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkGateHit>>>()))
            .Callback<Action<MessagePayload<NetworkGateHit>>>(handler => receive = handler);
        var machineState = new Mock<ISiegeMachineStateReplicator>();
        ConfigureAuthority(machineState, "owner-a", 7, 3);
        using var sut = new SiegeWeaponFireReplicator(Mock.Of<IBattleNetwork>(), broker.Object,
            Mock.Of<INetworkAgentRegistry>(), Mock.Of<IBattleSession>(), machineState.Object, Mock.Of<ISiegeGateHitApplier>(), new HostEpochPolicy());

        receive(new MessagePayload<NetworkGateHit>(this,
            new NetworkGateHit(44, ramId, 250, "owner-b", 7, 4)));
        DrainGameThread();
        Assert.Equal(1, PendingGateHitCount(sut));

        ConfigureAuthority(machineState, "owner-b", 7, 4);
        machineState.Raise(state => state.AuthorityChanged += null, ramId);
        DrainGameThread();
        Assert.Equal(1, PendingGateHitCount(sut));
        sut.Tick(0f);
        DrainGameThread();
        Assert.Equal(1, PendingGateHitCount(sut));

        ConfigureAuthority(machineState, "owner-a", 7, 5);
        machineState.Raise(state => state.AuthorityChanged += null, ramId);
        DrainGameThread();
        Assert.Equal(0, PendingGateHitCount(sut));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void BufferedGateHit_AppliesOnceAfterRegistration_AndNeverRetriesNativeFailure(
        bool isHost, bool throwsAfterApplication)
    {
        const int ramId = 43;
        using var missionScope = new MissionCurrentScope();
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        try
        {
            var broker = new Mock<IMessageBroker>();
            Action<MessagePayload<NetworkGateHit>> receive = null;
            broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkGateHit>>>()))
                .Callback<Action<MessagePayload<NetworkGateHit>>>(handler => receive = handler);
            var state = new Mock<ISiegeMachineStateReplicator>();
            ConfigureAuthority(state, "remote", 7, 4);
            var session = new Mock<IBattleSession>();
            session.SetupGet(x => x.IsLocalHost).Returns(isHost);
            var applier = new Mock<ISiegeGateHitApplier>();
            bool registered = false;
            int applications = 0;
            applier.Setup(x => x.TryApply(It.IsAny<NetworkGateHit>(), isHost))
                .Returns((NetworkGateHit message, bool damage) =>
                {
                    if (!registered) return false;
                    applications++;
                    if (throwsAfterApplication) throw new InvalidOperationException("after native apply");
                    return true;
                });
            using var sut = new SiegeWeaponFireReplicator(Mock.Of<IBattleNetwork>(), broker.Object,
                Mock.Of<INetworkAgentRegistry>(), session.Object, state.Object, applier.Object, new HostEpochPolicy());
            receive(new MessagePayload<NetworkGateHit>(this, new NetworkGateHit(44, ramId, 250, "remote", 7, 4)));
            DrainGameThread();
            Assert.Equal(1, PendingGateHitCount(sut));
            sut.Tick(0f);
            DrainGameThread();
            Assert.Equal(0, applications);
            Assert.Equal(1, PendingGateHitCount(sut));

            registered = true;
            state.Raise(x => x.AuthorityChanged += null, ramId);
            DrainGameThread();
            Assert.Equal(1, applications);
            Assert.Equal(0, PendingGateHitCount(sut));
            sut.Tick(0f);
            state.Raise(x => x.AuthorityChanged += null, ramId);
            DrainGameThread();
            Assert.Equal(1, applications);
            applier.Verify(x => x.TryApply(It.IsAny<NetworkGateHit>(), !isHost), Times.Never);
        }
        finally
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }
    }

    [Fact]
    public void ProjectileReplay_WithLocallyControlledShooter_DoesNotEnterNativeMissileSpawn()
    {
#pragma warning disable SYSLIB0050
        var shooter = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
#pragma warning restore SYSLIB0050
        var id = Guid.NewGuid();
        var info = new CoopAgentInfo("us", "us", "us", shooter, id, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(x => x.TryGetAgentInfo(id, out info)).Returns(true);
        registry.Setup(x => x.IsLocallyControlled(shooter)).Returns(true);
        using var sut = new SiegeWeaponFireReplicator(Mock.Of<IBattleNetwork>(), Mock.Of<IMessageBroker>(),
            registry.Object, Mock.Of<IBattleSession>(), Mock.Of<ISiegeMachineStateReplicator>(),
            Mock.Of<ISiegeGateHitApplier>(), new HostEpochPolicy());
        var message = new NetworkSiegeWeaponFired(42, id, default, default, default, 0f, 0f, "stone",
            "remote", 7, 4);
        var method = typeof(SiegeWeaponFireReplicator).GetMethod("TrySpawnProjectile",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.True((bool)method.Invoke(sut, new object[] { null, message }));
        registry.Verify(x => x.IsLocallyControlled(shooter), Times.Once);
    }

    private static int PendingGateHitCount(SiegeWeaponFireReplicator replicator)
    {
        var field = typeof(SiegeWeaponFireReplicator).GetField(
            "pendingGateHits", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var pending = Assert.IsType<Dictionary<int, List<NetworkGateHit>>>(field!.GetValue(replicator));
        return pending.Values.Sum(messages => messages.Count);
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
