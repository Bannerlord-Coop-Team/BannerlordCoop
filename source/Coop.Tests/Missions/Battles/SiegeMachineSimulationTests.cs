using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Missions;
using Moq;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.Engine;
using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeMachineSimulationTests : IDisposable
{
    private static readonly List<SynchedMissionObject> PausedSkeletons = new();
    private readonly Harmony harmony = new("coop.tests.siege-machine-simulation");

    public SiegeMachineSimulationTests()
    {
        harmony.Patch(
            AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeMachineSimulationTests), nameof(SkipScriptComponentCache))));
    }

    private static bool SkipScriptComponentCache() => false;

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        PausedSkeletons.Clear();
    }

    private static bool RecordPause(SynchedMissionObject __instance)
    {
        PausedSkeletons.Add(__instance);
        return false;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredMachine_DrainsBufferedStateAndRemotePresentation(bool ranged)
    {
        using var mission = new MissionCurrentScope();
        var broker = new TestMessageBroker();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.OwnControllerId).Returns("us");
        session.SetupGet(value => value.HostEpoch).Returns(5);
        session.Setup(value => value.IsHostController(It.IsAny<string>()))
            .Returns((string controller) => controller == "host");
        using var replicator = new SiegeMachineStateReplicator(Mock.Of<IBattleNetwork>(), broker,
            session.Object, Mock.Of<INetworkAgentRegistry>(), new HostEpochPolicy());
#pragma warning disable SYSLIB0050
        var machine = (UsableMachine)FormatterServices.GetUninitializedObject(
            ranged ? typeof(Ballista) : typeof(ManagedStonePile));
#pragma warning restore SYSLIB0050
        SynchedMissionObject skeleton = null;
        if (ranged)
        {
#pragma warning disable SYSLIB0050
            skeleton = (SynchedMissionObject)FormatterServices.GetUninitializedObject(typeof(SynchedMissionObject));
#pragma warning restore SYSLIB0050
            AccessTools.Field(typeof(RangedSiegeWeapon), "SkeletonOwnerObjects")
                .SetValue(machine, new[] { skeleton });
            harmony.Patch(AccessTools.Method(typeof(SynchedMissionObject), nameof(SynchedMissionObject.PauseSkeletonAnimationSynched)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeMachineSimulationTests), nameof(RecordPause))));
        }
        int weaponState = (int)RangedSiegeWeapon.WeaponState.ReloadingPaused;
        int id = machine.Id.Id;
        if (machine is ManagedStonePile initialPile) initialPile.SetAmmo(6);
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        try
        {
            broker.Publish(this, new NetworkSiegeMachineAuthority(id, "claimant", hostEpoch: 5,
                authorityRevision: 4, senderControllerId: "host"));
            broker.Publish(this, new NetworkSiegeMachineState(id, -1f, -1, -1, -1, -1f,
                false, ranged ? weaponState : -1, 0.75f, 0.25f, hostEpoch: 5,
                stoneAmmo: ranged ? -1 : 7, senderControllerId: "claimant", authorityRevision: 4));
            GameThread.Run(() => { }, blocking: true);
            var pending = ReadField<Dictionary<int, NetworkSiegeMachineState>>(replicator, "pendingByMachineId");
            Assert.Single(pending);
            Assert.False(SiegeMissionAuthorityGate.TryGetRemoteAim(id, out _, out _));
            var animations = ReadField<Dictionary<int, int>>(replicator, "peerWeaponState");
            Assert.Empty(animations);

            ReadField<Dictionary<int, UsableMachine>>(replicator, "machinesById")[id] = machine;
            var drain = typeof(SiegeMachineStateReplicator).GetMethod("DrainPendingMachineStates",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(drain);
            GameThread.Run(() => drain.Invoke(replicator, Array.Empty<object>()), blocking: true);

            Assert.Empty(pending);
            Assert.False(SiegeMissionAuthorityGate.SuppressCapture);
            Assert.True(SiegeMissionAuthorityGate.TryGetRemoteAim(id, out float direction, out float releaseAngle));
            Assert.Equal(0.75f, direction);
            Assert.Equal(0.25f, releaseAngle);
            if (machine is ManagedStonePile pile) Assert.Equal(7, pile.AmmoCount);
            else
            {
                Assert.Equal(weaponState, animations[id]);
                Assert.Same(skeleton, Assert.Single(PausedSkeletons));
            }
            SiegeMissionAuthorityGate.SetRemoteAim(id, 0.5f, 0.1f);
            GameThread.Run(() => drain.Invoke(replicator, Array.Empty<object>()), blocking: true);
            Assert.True(SiegeMissionAuthorityGate.TryGetRemoteAim(id, out direction, out _));
            Assert.Equal(0.5f, direction);
            if (ranged) Assert.Same(skeleton, Assert.Single(PausedSkeletons));
        }
        finally
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }
    }

    private static T ReadField<T>(SiegeMachineStateReplicator replicator, string name) =>
        (T)typeof(SiegeMachineStateReplicator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(replicator);

    [Theory]
    [InlineData(true, true, 6)]
    [InlineData(true, false, -1)]
    [InlineData(false, true, 6)]
    [InlineData(false, false, -1)]
    public void OutgoingStoneSnapshot_OnlyTheSimulatorPublishesAmmo(bool isHost, bool localSimulator, int expectedAmmo)
    {
#pragma warning disable SYSLIB0050
        var pile = (ManagedStonePile)FormatterServices.GetUninitializedObject(typeof(ManagedStonePile));
#pragma warning restore SYSLIB0050
        pile.SetAmmo(6);

        var snapshot = Capture(pile, isHost, localSimulator);

        Assert.Equal(pile.Id.Id, snapshot.MachineId);
        Assert.Equal(expectedAmmo, snapshot.StoneAmmo);
        Assert.Equal(localSimulator, snapshot.HasStoneAmmo);
        Assert.Equal(6, pile.AmmoCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OutgoingWeaponSnapshot_SeparatesHostDamageFromSimulatorAim(bool isHost, bool localSimulator)
    {
#pragma warning disable SYSLIB0050
        var weapon = (Ballista)FormatterServices.GetUninitializedObject(typeof(Ballista));
        var damage = (DestructableComponent)FormatterServices.GetUninitializedObject(typeof(DestructableComponent));
#pragma warning restore SYSLIB0050
        // These game members are not publicized by the test project; avoid native state-change setters.
        AccessTools.Field(typeof(RangedSiegeWeapon), "_state").SetValue(weapon, RangedSiegeWeapon.WeaponState.ReloadingPaused);
        AccessTools.Field(typeof(RangedSiegeWeapon), "TargetDirection").SetValue(weapon, 0.75f);
        AccessTools.Field(typeof(RangedSiegeWeapon), "TargetReleaseAngle").SetValue(weapon, 0.25f);
        AccessTools.Field(typeof(DestructableComponent), "_hitPoint").SetValue(damage, 37.5f);
        AccessTools.Field(typeof(DestructableComponent), "_currentStateIndex").SetValue(damage, 2);
        AccessTools.Property(typeof(UsableMachine), nameof(UsableMachine.DestructionComponent)).SetValue(weapon, damage);

        var snapshot = Capture(weapon, isHost, localSimulator);

        Assert.Equal(isHost ? 37.5f : -1f, snapshot.HitPoints);
        Assert.Equal(isHost ? 2 : -1, snapshot.DestructionState);
        Assert.Equal(localSimulator ? (int)RangedSiegeWeapon.WeaponState.ReloadingPaused : -1, snapshot.WeaponState);
        Assert.Equal(localSimulator ? 0.75f : -1000f, snapshot.AimDirection);
        Assert.Equal(localSimulator ? 0.25f : -1000f, snapshot.AimReleaseAngle);
        Assert.Equal(37.5f, damage.HitPoint);
        Assert.Equal(RangedSiegeWeapon.WeaponState.ReloadingPaused, weapon.State);
    }

    private static NetworkSiegeMachineState Capture(UsableMachine machine, bool isHost, bool localSimulator)
    {
        var capture = typeof(SiegeMachineStateReplicator).GetMethod("CaptureState", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(capture);
        return (NetworkSiegeMachineState)capture.Invoke(null, new object[] { machine, isHost, localSimulator });
    }

    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 7)]
    public void SnapshotAfterPickup_OnlyChangesAmmoOnTheRemoteSimulator(bool localSimulator, int expectedAmmo)
    {
#pragma warning disable SYSLIB0050
        var pile = (ManagedStonePile)FormatterServices.GetUninitializedObject(typeof(ManagedStonePile));
#pragma warning restore SYSLIB0050
        pile.SetAmmo(6);
        int machineId = pile.Id.Id;
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.SetClaimedMachines(
            localSimulator ? new HashSet<int> { machineId } : new HashSet<int>(), new HashSet<int>());
        try
        {
            var snapshot = new NetworkSiegeMachineState(machineId, -1f, -1, -1, -1, -1f,
                false, -1, -1000f, -1000f, stoneAmmo: 7);
            var apply = typeof(SiegeMachineStateReplicator).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(apply);
            apply.Invoke(null, new object[] { pile, snapshot });
            Assert.Equal(expectedAmmo, pile.AmmoCount);
        }
        finally
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void RamTick_UsesReplicaBranchUntilItsLocalAuthorityIsKnown(
        bool authorityKnown, bool localSimulator, bool replicaBranch)
    {
#pragma warning disable SYSLIB0050
        var ram = (BatteringRam)FormatterServices.GetUninitializedObject(typeof(BatteringRam));
#pragma warning restore SYSLIB0050
        bool previousKnown = SiegeMissionAuthorityGate.IsAuthorityKnown;
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        bool previousEnabled = BattleSpawnConfig.Enabled;
        BattleSpawnConfig.Enabled = true;
        BattleSpawnGate.BeginBattle("ram-authority-test");
        SiegeMissionAuthorityGate.IsAuthorityKnown = authorityKnown;
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.SetClaimedMachines(
            localSimulator ? new HashSet<int> { ram.Id.Id } : new HashSet<int>(), new HashSet<int>());
        try
        {
            Assert.False(GameNetwork.IsClientOrReplay);
            var patchType = typeof(BattleSpawnGate).Assembly
                .GetType("GameInterface.Services.MapEvents.Patches.SiegeMachineAuthorityPatches");
            var check = patchType.GetMethod("IsClientForMachine", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(check);
            Assert.Equal(replicaBranch, (bool)check.Invoke(null, new object[] { ram }));
            BattleSpawnGate.EndBattle();
            Assert.False((bool)check.Invoke(null, new object[] { ram }));
        }
        finally
        {
            BattleSpawnGate.EndBattle();
            BattleSpawnConfig.Enabled = previousEnabled;
            SiegeMissionAuthorityGate.IsAuthorityKnown = previousKnown;
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }
    }

    private sealed class ManagedStonePile : StonePile
    {
        protected override void UpdateAmmoMesh() { }
        protected override void CheckAmmo() { }
    }
}
