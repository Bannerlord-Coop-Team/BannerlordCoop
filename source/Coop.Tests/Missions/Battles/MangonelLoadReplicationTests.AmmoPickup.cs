using Common.Messaging;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Agents.Messages;
using Missions.Battles;
using Missions.Messages;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public sealed partial class MangonelLoadReplicationTests
{
    private (SiegeMachineStateReplicator Sut, CoopAgentInfo Info, Mangonel Machine, Mock<IBattleNetwork> Network)
        SupplyReplica(string own, Guid? pickerId = null, string agentOwner = "loader")
    {
        var replica = Replica(own, pickerId, agentOwner);
        var session = Mock.Get(Field<IBattleSession>(replica.Sut, "session"));
        session.SetupGet(s => s.HostControllerId).Returns("host");
        session.SetupGet(s => s.IsLocalHost).Returns(own == "host");
        session.Setup(s => s.IsHostController("host")).Returns(true);
        var registry = Mock.Get(Field<INetworkAgentRegistry>(replica.Sut, "agentRegistry"));
        registry.Setup(r => r.IsLocallyControlled(replica.Info.Agent)).Returns(own == agentOwner);
        replica.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot] = default;
        var pickup = New<StandingPoint>();
        AccessTools.Property(typeof(MissionObject), "Id").SetValue(pickup, new MissionObjectId(1400, false));
        AccessTools.Property(typeof(UsableMachine), "AmmoPickUpPoints").SetValue(replica.Machine,
            new List<StandingPoint> { pickup });
        replica.Machine.StartingAmmoCount = 2;
        AccessTools.Property(typeof(RangedSiegeWeapon), "AmmoCount").SetValue(replica.Machine, 1);
        AccessTools.Property(typeof(RangedSiegeWeapon), "HasAmmo").SetValue(replica.Machine, true);
        AccessTools.Field(typeof(SiegeMachineStateReplicator), "trackedMission").SetValue(replica.Sut, mission.Instance);
        AccessTools.Field(typeof(SiegeMachineStateReplicator), "trackedObjectCount").SetValue(replica.Sut, 0);
        Stub(typeof(RangedSiegeWeapon), "UpdateAmmoMesh", nameof(Skip));
        Stub(typeof(SiegeWeapon), "SetForcedUse", nameof(Skip));
        Stub(typeof(Agent), nameof(Agent.EquipWeaponToExtraSlotAndWield), nameof(PickupEquip));
        Use(replica.Info.Agent, pickup);
        return replica;
    }

    private static NetworkMangonelAmmoPickup RequestPickup(SiegeMachineStateReplicator sut, CoopAgentInfo info, Mangonel machine)
    {
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "HandleMangonelAmmoPickup").Invoke(sut,
            new object[] { new MessagePayload<MangonelAmmoPickup>(machine, new MangonelAmmoPickup(info.Agent, machine)) });
        return Field<Dictionary<Guid, NetworkMangonelAmmoPickup>>(sut, "localMangonelPickups")[info.AgentId];
    }

    private static NetworkMangonelAmmoPickup Decision(Mock<IBattleNetwork> network, Guid requestId) =>
        network.Invocations.SelectMany(i => i.Arguments).OfType<NetworkMangonelAmmoPickup>()
            .Last(m => m.RequestId == requestId && m.Phase != MangonelPickupPhase.Request);

    private static StandingPoint PickupPoint(Mangonel machine) =>
        ((List<StandingPoint>)AccessTools.Property(typeof(UsableMachine), "AmmoPickUpPoints").GetValue(machine))[0];

    [Fact]
    public void LastBoulder_TwoClientsRequestBeforeEitherReply_OnlyOneGrantAndBothPickupsDeactivate()
    {
        var host = SupplyReplica("host");
        var first = SupplyReplica("loader");
        var second = SupplyReplica("other", Guid.NewGuid(), "other");
        var secondInfo = second.Info;
        Mock.Get(Field<INetworkAgentRegistry>(host.Sut, "agentRegistry"))
            .Setup(r => r.TryGetAgentInfo(secondInfo.AgentId, out secondInfo)).Returns(true);
        var one = Serializer.DeepClone(RequestPickup(first.Sut, first.Info, first.Machine));
        var two = Serializer.DeepClone(RequestPickup(second.Sut, second.Info, second.Machine));
        Assert.True(first.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.True(second.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        host.Sut.ApplyMangonelPickup(one);
        host.Sut.ApplyMangonelPickup(two);
        var granted = Serializer.DeepClone(Decision(host.Network, one.RequestId));
        var denied = Serializer.DeepClone(Decision(host.Network, two.RequestId));
        Assert.Equal(MangonelPickupPhase.Granted, granted.Phase);
        Assert.Equal(MangonelPickupPhase.Denied, denied.Phase);
        Assert.Equal(0, host.Machine.AmmoCount);
        Stop(first.Info.Agent);
        Stop(second.Info.Agent);
        first.Sut.ApplyMangonelPickup(granted);
        second.Sut.ApplyMangonelPickup(denied);
        Assert.Same(missile, first.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Item);
        Assert.True(second.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.All(new[] { host.Machine, first.Machine, second.Machine }, machine =>
        {
            Assert.Equal(0, machine.AmmoCount);
            Assert.True(PickupPoint(machine).IsDeactivated);
        });
        Mock.Get(Field<IMessageBroker>(first.Sut, "messageBroker"))
            .Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<LadderForkGranted>()), Times.Once);
        host.Sut.ApplyMangonelPickup(one);
        first.Sut.ApplyMangonelPickup(granted);
        Assert.Equal(0, host.Machine.AmmoCount);
        Mock.Get(Field<IMessageBroker>(first.Sut, "messageBroker"))
            .Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<LadderForkGranted>()), Times.Once);
    }

    [Fact]
    public void SupplyCatchUp_IsHostOwnedEvenWhenPilotClaimsMachine_AndNeverRefillsFromAnOlderCount()
    {
        var host = SupplyReplica("host");
        var joiner = SupplyReplica("loader");
        var capture = AccessTools.Method(typeof(SiegeMachineStateReplicator), "CaptureState");
        var before = (NetworkSiegeMachineState)capture.Invoke(null, new object[] { host.Machine, true, false });
        Assert.True(before.HasMangonelAmmo);
        var pilot = (NetworkSiegeMachineState)capture.Invoke(null, new object[] { host.Machine, false, true });
        Assert.False(pilot.HasMangonelAmmo);
        host.Sut.ApplyMangonelPickup(new NetworkMangonelAmmoPickup("battle", Guid.NewGuid(), agentId,
            "loader", 7, host.Info.SiegeEquipmentGrantRevision, 1496, 1400, "host", 3));
        var after = Serializer.DeepClone((NetworkSiegeMachineState)capture.Invoke(null, new object[] { host.Machine, true, false }));
        Assert.True(after.HasMangonelAmmo);
        Assert.Equal(0, after.MangonelAmmo);
        var apply = AccessTools.Method(typeof(SiegeMachineStateReplicator), "Apply");
        apply.Invoke(null, new object[] { joiner.Machine, after });
        apply.Invoke(null, new object[] { joiner.Machine, before });
        Assert.Equal(0, joiner.Machine.AmmoCount);
        Assert.True(PickupPoint(joiner.Machine).IsDeactivated);
    }

    [Theory]
    [InlineData("other-battle", "host", 3, "loader", 7)]
    [InlineData("battle", "old-host", 3, "loader", 7)]
    [InlineData("battle", "host", 2, "loader", 7)]
    [InlineData("battle", "host", 3, "other", 7)]
    [InlineData("battle", "host", 3, "loader", 6)]
    public void StalePickup_DoesNotConsumeSupply(string battle, string hostId, int epoch, string owner, long revision)
    {
        var host = SupplyReplica("host");
        host.Sut.ApplyMangonelPickup(new NetworkMangonelAmmoPickup(battle, Guid.NewGuid(), agentId,
            owner, revision, host.Info.SiegeEquipmentGrantRevision, 1496, 1400, hostId, epoch));
        Assert.Equal(1, host.Machine.AmmoCount);
        Assert.Empty(host.Network.Invocations);
    }

    [Fact]
    public void LateRegistration_RetainsPickupUntilAgentIsKnown_ThenConsumesOnce()
    {
        var host = SupplyReplica("host");
        var info = host.Info;
        var registry = Mock.Get(Field<INetworkAgentRegistry>(host.Sut, "agentRegistry"));
        bool registered = false;
        registry.Setup(r => r.TryGetAgentInfo(agentId, out info)).Returns(() => registered);
        var request = new NetworkMangonelAmmoPickup("battle", Guid.NewGuid(), agentId,
            "loader", 7, info.SiegeEquipmentGrantRevision, 1496, 1400, "host", 3);
        host.Sut.ApplyMangonelPickup(request);
        Assert.Equal(1, host.Machine.AmmoCount);
        registered = true;
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "TickMangonelPickups").Invoke(host.Sut, null);
        Assert.Equal(0, host.Machine.AmmoCount);
        Assert.Equal(MangonelPickupPhase.Granted, Decision(host.Network, request.RequestId).Phase);
        Assert.Empty(Field<Dictionary<Guid, NetworkMangonelAmmoPickup>>(host.Sut, "pendingMangonelPickups"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LateApproval_DoesNotOverwriteANewerGrantOrAnAgentAuthorityChange(bool newerGrant)
    {
        var picker = SupplyReplica("loader");
        var request = RequestPickup(picker.Sut, picker.Info, picker.Machine);
        if (newerGrant) picker.Info.RecordSiegeGrant(Guid.NewGuid(), request.GrantRevision + 1);
        else AccessTools.Property(typeof(CoopAgentInfo), nameof(CoopAgentInfo.AuthorityRevision)).SetValue(picker.Info, 8L);
        picker.Sut.ApplyMangonelPickup(request.Decide(true, 0));
        Assert.True(picker.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.Equal(0, picker.Machine.AmmoCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeFinitePickup_InstalledPatchReservesOnHostAndConsumesExactlyOnce(bool localHost)
    {
        string owner = localHost ? "host" : "loader";
        var picker = SupplyReplica(owner, agentOwner: owner);
        SetState(picker.Machine, RangedSiegeWeapon.WeaponState.Idle);
        CurrentActions[picker.Info.Agent] = ActionIndexCache.act_pickup_boulder_end;
        Stub(typeof(RangedSiegeWeapon), "OnTick", nameof(BaseTick));
        Stub(typeof(WeakGameEntity), "IsVisibleIncludeParents", nameof(Active));
        harmony.Patch(AccessTools.PropertyGetter(typeof(ScriptComponentBehavior), "GameEntity"),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(Entity)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsAIControlled)),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(NotAi)));
        var category = typeof(MangonelAmmoPickupPatch).GetCustomAttributes(typeof(HarmonyPatchCategory), false)
            .Cast<HarmonyPatchCategory>().Single();
        Assert.Equal(MissionModule.WeaponPickupPatchCategory, category.info.category);
        harmony.CreateClassProcessor(typeof(MangonelAmmoPickupPatch)).Patch();
        var repeatedHarmony = new Harmony(harmony.Id + ".repeated");
        repeatedHarmony.CreateClassProcessor(typeof(MangonelAmmoPickupPatch)).Patch();
        Assert.Contains(Harmony.GetPatchInfo(AccessTools.DeclaredMethod(typeof(Mangonel), "OnTick")).Transpilers,
            p => p.PatchMethod.DeclaringType == typeof(MangonelAmmoPickupPatch));
        bool oldEnabled = BattleSpawnConfig.Enabled;
        Action<MessagePayload<MangonelAmmoPickup>> receive = payload =>
            AccessTools.Method(typeof(SiegeMachineStateReplicator), "HandleMangonelAmmoPickup").Invoke(picker.Sut,
                new object[] { payload });
        MessageBroker.Instance.Subscribe(receive);
        BattleSpawnConfig.Enabled = true;
        BattleSpawnGate.BeginBattle("finite-mangonel-pickup");
        try
        {
            AccessTools.DeclaredMethod(typeof(Mangonel), "OnTick").Invoke(picker.Machine, new object[] { 0.1f });
            Assert.Equal(!localHost, picker.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
            Assert.Null(picker.Info.Agent.CurrentlyUsedGameObject);
            Assert.Equal(localHost ? 0 : 1, picker.Machine.AmmoCount);
            Assert.Single(picker.Network.Invocations.SelectMany(i => i.Arguments).OfType<NetworkMangonelAmmoPickup>());
        }
        finally
        {
            repeatedHarmony.UnpatchAll(repeatedHarmony.Id);
            MessageBroker.Instance.Unsubscribe(receive);
            BattleSpawnGate.EndBattle();
            BattleSpawnConfig.Enabled = oldEnabled;
        }
    }
}
