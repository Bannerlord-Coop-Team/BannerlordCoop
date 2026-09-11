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

    [Fact]
    public void ReplicatedRamArrival_EnablesPullPointsBeforeClaimingWithoutReplicaStrikes()
    {
        using var mission = new MissionCurrentScope();
        var ram = Uninitialized<BatteringRam>();
        var movement = Uninitialized<SiegeWeaponMovementComponent>();
        var gate = Uninitialized<CastleGate>();
        var pull = Uninitialized<StandingPoint>();
        ramMovePoint = Uninitialized<StandingPoint>();
        ramScene = Uninitialized<Scene>();
        ramMovementArrived = false;
        ramStrikeReads = 0;
        ramDisabledNavMesh = 0;
        AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(pull, true);
        AccessTools.Property(typeof(UsableMachine), nameof(UsableMachine.StandingPoints)).SetValue(ram,
            new TaleWorlds.Library.MBList<StandingPoint> { pull, ramMovePoint });
        AccessTools.Property(typeof(BatteringRam), nameof(BatteringRam.MovementComponent)).SetValue(ram, movement);
        AccessTools.Field(typeof(BatteringRam), "_gate").SetValue(ram, gate);
        ram.DisabledNavMeshID = 8;
        var patchType = typeof(BattleSpawnGate).Assembly
            .GetType("GameInterface.Services.MapEvents.Patches.SiegeMachineAuthorityPatches");
        var tick = AccessTools.DeclaredMethod(typeof(BatteringRam), "OnTick");
        Stub(AccessTools.Method(typeof(SiegeWeapon), "OnTick"), nameof(SkipScriptComponentCache));
        Stub(AccessTools.PropertyGetter(typeof(ScriptComponentBehavior), "GameEntity"), nameof(RamEntity));
        Stub(AccessTools.Method(typeof(WeakGameEntity), "IsVisibleIncludeParents"), nameof(RamVisible));
        Stub(AccessTools.Method(typeof(WeakGameEntity), "HasTag"), nameof(RamTag));
        Stub(AccessTools.PropertyGetter(typeof(WeakGameEntity), "Scene"), nameof(RamScene));
        Stub(AccessTools.Method(typeof(Scene), "SetAbilityOfFacesWithId"), nameof(RamNavigation));
        Stub(AccessTools.PropertySetter(typeof(UsableMissionObject), "IsDeactivated"), nameof(RamPointActivation));
        Stub(AccessTools.PropertyGetter(typeof(CastleGate), "IsDestroyed"), nameof(RamGateIntact));
        Stub(AccessTools.PropertyGetter(typeof(CastleGate), "IsGateOpen"), nameof(RamGateIntact));
        Stub(AccessTools.PropertyGetter(typeof(UsableMachine), "UserCountNotInStruckAction"), nameof(RamStrikeCount));
        Stub(AccessTools.PropertyGetter(typeof(SiegeWeaponMovementComponent), "HasArrivedAtTarget"), nameof(RamMovementArrived));
        Stub(AccessTools.Method(typeof(SiegeWeaponMovementComponent), "GetTotalDistanceTraveledForPathTracker"), nameof(RamDistance));
        Stub(AccessTools.Method(typeof(SiegeWeaponMovementComponent), "SetDestinationNavMeshIdState"), nameof(SkipScriptComponentCache));
        Stub(AccessTools.Method(typeof(SiegeWeaponMovementComponent), "MoveToTargetAsClient"), nameof(RamMoveToTarget));
        harmony.Patch(tick, transpiler: new HarmonyMethod(AccessTools.Method(patchType, "RamOnTickTranspiler")));

        bool oldHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        bool oldKnown = SiegeMissionAuthorityGate.IsAuthorityKnown;
        bool oldEnabled = BattleSpawnConfig.Enabled;
        BattleSpawnConfig.Enabled = true;
        BattleSpawnGate.BeginBattle("ram-arrival-test");
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.IsAuthorityKnown = true;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        try
        {
            var broker = new TestMessageBroker();
            var sent = new List<IMessage>();
            var network = new Mock<IBattleNetwork>();
            network.Setup(value => value.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
            var session = new Mock<IBattleSession>();
            session.SetupGet(value => value.OwnControllerId).Returns("peer");
            session.SetupGet(value => value.HostEpoch).Returns(5);
            session.Setup(value => value.IsHostController("host")).Returns(true);
            using var replicator = new SiegeMachineStateReplicator(network.Object, broker, session.Object,
                Mock.Of<INetworkAgentRegistry>(), new HostEpochPolicy());
            GameThread.Run(() => AccessTools.Method(typeof(SiegeMachineStateReplicator), "RefreshMachineCache")
                .Invoke(replicator, Array.Empty<object>()), blocking: true);
            ReadField<Dictionary<int, UsableMachine>>(replicator, "machinesById")[ram.Id.Id] = ram;
            ReadField<List<UsableMachine>>(replicator, "machines").Add(ram);
            broker.Publish(this, new NetworkSiegeMachineState(ram.Id.Id, -1f, -1, -1, -1, 0f,
                true, -1, -1000f, -1000f, hostEpoch: 5, senderControllerId: "host"));
            GameThread.Run(() => { }, blocking: true);
            Assert.True(ramMovementArrived);
            Assert.False(ram.HasArrivedAtTarget);
            Assert.True(pull.IsDeactivated);
            tick.Invoke(ram, new object[] { 0.1f });
            Assert.True(ram.HasArrivedAtTarget);
            Assert.False(pull.IsDeactivated);
            Assert.True(ramMovePoint.IsDeactivated);
            Assert.Equal(8, ramDisabledNavMesh);
            Assert.Equal(0, ramStrikeReads);

            // The native seat is now usable; model the local player's completed mount, then run the real claim scan.
            var player = Uninitialized<Agent>();
            Stub(AccessTools.PropertyGetter(typeof(Agent), "IsMine"), nameof(RamVisible));
            AccessTools.Field(typeof(UsableMissionObject), "_userAgent").SetValue(pull, player);
            AccessTools.Method(typeof(SiegeMachineStateReplicator), "ScanMachineClaims").Invoke(replicator, new object[] { 0.1f });
            var claim = Assert.IsType<NetworkSiegeMachineClaim>(Assert.Single(sent));
            Assert.Equal(ram.Id.Id, claim.MachineId);
            tick.Invoke(ram, new object[] { 0.1f });
            Assert.Equal(0, ramStrikeReads);

            broker.Publish(this, new NetworkSiegeMachineAuthority(ram.Id.Id, "peer", hostEpoch: 5,
                authorityRevision: 1, senderControllerId: "host"));
            GameThread.Run(() => { }, blocking: true);
            Assert.True(SiegeMissionAuthorityGate.IsMachineSimulatedLocally(ram.Id.Id));
            tick.Invoke(ram, new object[] { 0.1f });
            Assert.Equal(1, ramStrikeReads);
            Assert.False(pull.IsDeactivated);
            Assert.True(ramMovePoint.IsDeactivated);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
            BattleSpawnConfig.Enabled = oldEnabled;
            SiegeMissionAuthorityGate.IsLocalAuthority = oldHost;
            SiegeMissionAuthorityGate.IsAuthorityKnown = oldKnown;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
            ramMovePoint = null;
            ramScene = null;
            ramEntityOwner = null;
        }
    }

    private void Stub(MethodInfo method, string prefix)
    {
        Assert.NotNull(method);
        harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeMachineSimulationTests), prefix)));
    }

#pragma warning disable SYSLIB0050
    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
    private static StandingPoint ramMovePoint;
    private static ScriptComponentBehavior ramEntityOwner;
    private static Scene ramScene;
    private static bool ramMovementArrived;
    private static int ramStrikeReads;
    private static int ramDisabledNavMesh;
    private static bool RamEntity(ScriptComponentBehavior __instance, ref WeakGameEntity __result)
    {
        ramEntityOwner = __instance;
        __result = default;
        return false;
    }
    private static bool RamVisible(ref bool __result) { __result = true; return false; }
    private static bool RamGateIntact(ref bool __result) { __result = false; return false; }
    private static bool RamTag(string __0, ref bool __result)
    {
        __result = __0 == "move" && ReferenceEquals(ramEntityOwner, ramMovePoint);
        return false;
    }
    private static bool RamScene(ref Scene __result) { __result = ramScene; return false; }
    private static bool RamNavigation(int __0, bool __1)
    {
        Assert.False(__1);
        ramDisabledNavMesh = __0;
        return false;
    }
    private static bool RamPointActivation(UsableMissionObject __instance, bool __0)
    {
        AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(__instance, __0);
        return false;
    }
    private static bool RamStrikeCount(ref int __result) { ramStrikeReads++; __result = 0; return false; }
    private static bool RamMovementArrived(ref bool __result) { __result = ramMovementArrived; return false; }
    private static bool RamDistance(ref float __result) { __result = 0f; return false; }
    private static bool RamMoveToTarget() { ramMovementArrived = true; return false; }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(5, false, false)]
    [InlineData(0, true, false)]
    [InlineData(5, true, false)]
    [InlineData(0, true, true)]
    [InlineData(5, true, true)]
    public void HostPickupBeforePeerClaim_TransfersAmmoBeforeTheClaimantSimulates(
        int hostEpoch, bool loading, bool liveStateCompletesLoading)
    {
        using var mission = new MissionCurrentScope();
        var hostBroker = new TestMessageBroker();
        var peerBroker = new TestMessageBroker();
        var observerBroker = new TestMessageBroker();
        var hostMessages = new List<IMessage>();
        var peerMessages = new List<IMessage>();
        using var host = CreateReplica("host", true, hostBroker, hostMessages);
        using var peer = CreateReplica("peer", false, peerBroker, peerMessages);
        using var observer = CreateReplica("observer", false, observerBroker, new List<IMessage>());
        var hostPile = RegisterPile(host);
        var peerPile = RegisterPile(peer);
        var observerPile = RegisterPile(observer);
        int id = hostPile.Id.Id;
        if (loading)
        {
            ReadField<Dictionary<int, UsableMachine>>(peer, "machinesById").Clear();
            ReadField<Dictionary<int, UsableMachine>>(observer, "machinesById").Clear();
        }
        bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
        try
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = true;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
            Broadcast(host, hostPile, true, true);
            var staleTen = Assert.IsType<NetworkSiegeMachineState>(Assert.Single(hostMessages));
            Assert.Equal(10, staleTen.StoneAmmo);
            hostMessages.Clear();

            GameThread.Run(() => hostPile.PickUpStone(), blocking: true);
            Assert.Equal(9, hostPile.AmmoCount);
            hostBroker.Publish(this, new NetworkSiegeMachineClaim(id, "peer", isRelease: false));
            GameThread.Run(() => { }, blocking: true);

            var grant = ProtoBuf.Serializer.DeepClone(
                Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(hostMessages)));
            Assert.True(grant.HasStoneAmmo);
            Assert.Equal(9, grant.StoneAmmo);
            Assert.Equal(1, grant.AuthorityRevision);
            Assert.False(Capture(hostPile, true, false).HasStoneAmmo);

            ReceiveHandoff(peer, peerBroker, peerPile, true);

            SiegeMissionAuthorityGate.IsLocalAuthority = false;
            SiegeMissionAuthorityGate.SetClaimedMachines(new HashSet<int> { id }, new HashSet<int>());
            Broadcast(peer, peerPile, false, true);
            var firstPeerState = Assert.IsType<NetworkSiegeMachineState>(Assert.Single(peerMessages));
            Assert.Equal(9, firstPeerState.StoneAmmo);
            Assert.Equal(grant.AuthorityRevision, firstPeerState.AuthorityRevision);
            ReceivePeerState(firstPeerState, 9, includeObserver: false);
            peerMessages.Clear();

            SiegeMissionAuthorityGate.IsLocalAuthority = false;
            SiegeMissionAuthorityGate.SetClaimedMachines(new HashSet<int> { id }, new HashSet<int>());
            GameThread.Run(() => peerPile.PickUpStone(), blocking: true);
            Broadcast(peer, peerPile, false, true);
            var nextPeerState = Assert.IsType<NetworkSiegeMachineState>(Assert.Single(peerMessages));
            Assert.Equal(8, peerPile.AmmoCount);
            ReceivePeerState(nextPeerState, 8, includeObserver: false);

            // The observer can receive the claimant's pickup before the host's grant on another connection.
            SiegeMissionAuthorityGate.IsLocalAuthority = false;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
            observerBroker.Publish(this, nextPeerState);
            GameThread.Run(() => { }, blocking: true);
            Assert.Equal(10, observerPile.AmmoCount);
            ReceiveHandoff(observer, observerBroker, observerPile, false, expectedAmmo: 8);
            ReceivePeerState(nextPeerState, 8);
            peerBroker.Publish(this, grant);
            GameThread.Run(() => { }, blocking: true);
            Assert.Equal(8, peerPile.AmmoCount);

            void ReceiveHandoff(SiegeMachineStateReplicator replicator, TestMessageBroker target,
                ManagedStonePile pile, bool ownsPile, int expectedAmmo = 9)
            {
                SiegeMissionAuthorityGate.IsLocalAuthority = false;
                SiegeMissionAuthorityGate.ResetClaimedMachines();
                target.Publish(this, grant);
                GameThread.Run(() => { }, blocking: true);
                if (loading)
                {
                    Assert.Equal(10, pile.AmmoCount);
                    Assert.False(SiegeMissionAuthorityGate.IsMachineSimulatedLocally(id));
                    ReadField<Dictionary<int, UsableMachine>>(replicator, "machinesById")[id] = pile;
                    if (liveStateCompletesLoading)
                    {
                        target.Publish(this, new NetworkSiegeMachineState(id, -1f, -1, -1, -1, -1f,
                            false, -1, -1000f, -1000f, hostEpoch: hostEpoch,
                            senderControllerId: "host", authorityRevision: grant.AuthorityRevision));
                        GameThread.Run(() => { }, blocking: true);
                    }
                    else
                    {
                        GameThread.Run(() => AccessTools.Method(typeof(SiegeMachineStateReplicator), "DrainPendingMachineStates")
                            .Invoke(replicator, Array.Empty<object>()), blocking: true);
                    }
                }
                Assert.Equal(expectedAmmo, pile.AmmoCount);
                Assert.Equal(ownsPile, SiegeMissionAuthorityGate.IsMachineSimulatedLocally(id));
                target.Publish(this, staleTen);
                GameThread.Run(() => { }, blocking: true);
                Assert.Equal(expectedAmmo, pile.AmmoCount);
            }

            void ReceivePeerState(NetworkSiegeMachineState state, int expected, bool includeObserver = true)
            {
                SiegeMissionAuthorityGate.IsLocalAuthority = true;
                SiegeMissionAuthorityGate.SetClaimedMachines(new HashSet<int>(), new HashSet<int> { id });
                hostBroker.Publish(this, state);
                GameThread.Run(() => { }, blocking: true);
                Assert.Equal(expected, hostPile.AmmoCount);
                if (!includeObserver) return;
                SiegeMissionAuthorityGate.IsLocalAuthority = false;
                SiegeMissionAuthorityGate.ResetClaimedMachines();
                observerBroker.Publish(this, state);
                GameThread.Run(() => { }, blocking: true);
                Assert.Equal(expected, observerPile.AmmoCount);
            }
        }
        finally
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }

        SiegeMachineStateReplicator CreateReplica(string controller, bool isHost,
            TestMessageBroker broker, List<IMessage> messages)
        {
            var session = new Mock<IBattleSession>();
            session.SetupGet(value => value.OwnControllerId).Returns(controller);
            session.SetupGet(value => value.IsLocalHost).Returns(isHost);
            session.SetupGet(value => value.HostEpoch).Returns(hostEpoch);
            session.Setup(value => value.IsHostController(It.IsAny<string>()))
                .Returns((string value) => value == "host");
            var network = new Mock<IBattleNetwork>();
            network.Setup(value => value.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(messages.Add);
            return new SiegeMachineStateReplicator(network.Object, broker, session.Object,
                Mock.Of<INetworkAgentRegistry>(), new HostEpochPolicy());
        }

        ManagedStonePile RegisterPile(SiegeMachineStateReplicator replicator)
        {
#pragma warning disable SYSLIB0050
            var pile = (ManagedStonePile)FormatterServices.GetUninitializedObject(typeof(ManagedStonePile));
#pragma warning restore SYSLIB0050
            AccessTools.Property(typeof(UsableMachine), nameof(UsableMachine.StandingPoints))
                .SetValue(pile, new TaleWorlds.Library.MBList<StandingPoint>());
            pile.SetAmmo(10);
            GameThread.Run(() => AccessTools.Method(typeof(SiegeMachineStateReplicator), "RefreshMachineCache")
                .Invoke(replicator, Array.Empty<object>()), blocking: true);
            ReadField<Dictionary<int, UsableMachine>>(replicator, "machinesById")[pile.Id.Id] = pile;
            return pile;
        }

        void Broadcast(SiegeMachineStateReplicator replicator, ManagedStonePile pile, bool isHost, bool local)
        {
            GameThread.Run(() => AccessTools.Method(typeof(SiegeMachineStateReplicator), "BroadcastMachineStateIfChanged")
                .Invoke(replicator, new object[] { Capture(pile, isHost, local) }), blocking: true);
        }
    }

    private sealed class ManagedStonePile : StonePile
    {
        public void PickUpStone() => ConsumeAmmo();
        protected override void UpdateAmmoMesh() { }
        protected override void CheckAmmo() { }
    }
}
