using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Agents.Patches;
using Missions.Battles;
using Missions.Data;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class LadderForkReplicationTests : IDisposable
{
    private readonly MissionCurrentScope mission = new();
    private readonly Harmony harmony = new("coop.tests.fork-grant." + Guid.NewGuid());
    private readonly Mock<INetworkAgentRegistry> registry = new();
    private readonly Mock<IMessageBroker> broker = new();
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly Agent agent = New<Agent>();
    private readonly ItemObject item = new("push_fork");
    private readonly CoopAgentInfo info;
    private readonly WeaponPickupHandler handler;
    private Action<MessagePayload<NetworkLadderForkGranted>> receive;
    private Action<MessagePayload<LadderForkGranted>> grant;
    private static readonly List<string> Calls = new();

    public LadderForkReplicationTests()
    {
        Calls.Clear();
        item.AddWeapon(new WeaponComponentData(null, WeaponClass.OneHandedPolearm, default), null);
        AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, mission.Instance);
        AccessTools.Property(typeof(Agent), "Equipment").SetValue(agent, new MissionEquipment());
        info = new CoopAgentInfo("actor", "actor", "scope", agent, Guid.NewGuid(), 1, 7);
        var registered = info;
        var registeredItem = item;
        registry.Setup(r => r.TryGetAgentInfo(info.AgentId, out registered)).Returns(true);
        registry.Setup(r => r.TryGetAgentInfo(agent, out registered)).Returns(true);
        objects.Setup(o => o.TryGetObject("fork-id", out registeredItem)).Returns(true);
        objects.Setup(o => o.TryGetObjectWithLogging("fork-id", out registeredItem)).Returns(true);
        string itemId = "fork-id";
        objects.Setup(o => o.TryGetId(item, out itemId)).Returns(true);
        objects.Setup(o => o.TryGetIdWithLogging(item, out itemId)).Returns(true);
        broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkLadderForkGranted>>>()))
            .Callback<Action<MessagePayload<NetworkLadderForkGranted>>>(value => receive = value);
        broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<LadderForkGranted>>>()))
            .Callback<Action<MessagePayload<LadderForkGranted>>>(value => grant = value);
        Stub(typeof(Agent), nameof(Agent.IsActive), nameof(Active));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsHuman)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(LadderForkReplicationTests), nameof(Active))));
        Stub(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex), nameof(MainHand));
        Stub(typeof(Agent), nameof(Agent.GetOffhandWieldedItemIndex), nameof(OffHand));
        Stub(typeof(Agent), nameof(Agent.EquipWeaponToExtraSlotAndWield), nameof(Equip));
        Stub(typeof(AgentEquipmentData), nameof(AgentEquipmentData.Apply), nameof(Wield));
        Stub(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents", nameof(Skip));
        handler = new WeaponPickupHandler(registry.Object, Mock.Of<INetworkWorldItemRegistry>(),
            network.Object, broker.Object, objects.Object);
    }

    [Fact]
    public void Grant_RoundTripsAuthorityAndZeroAmountFork()
    {
        var message = Message();
        var copy = Serializer.DeepClone(message);
        Assert.Equal(message.GrantId, copy.GrantId);
        Assert.Equal(info.AgentId, copy.AgentId);
        Assert.Equal("actor", copy.Authority);
        Assert.Equal(7, copy.AuthorityRevision);
        Assert.Equal("fork-id", copy.ItemObjectId);
        Assert.Equal(0, copy.DataValue);
        Assert.Equal((int)EquipmentIndex.ExtraWeaponSlot, copy.Equipment.MainHandIndex);
    }

    [Fact]
    public void Receive_EquipsBeforeWieldAndDuplicateAfterDropCannotResurrectFork()
    {
        var message = Message();
        Receive(message);
        Assert.Same(item, agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Item);
        Assert.Equal(new[] { "equip", "wield" }, Calls);
        SetExtra(agent, default); // A later authoritative drop has cleared the slot.
        Receive(message);
        Assert.True(agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.Equal(2, Calls.Count);
        broker.Verify(b => b.Publish(It.IsAny<object>(), It.Is<WeaponPickupApplied>(p =>
            p.AgentId == info.AgentId && p.WorldItemId == Guid.Empty &&
            p.EquipmentIndex == EquipmentIndex.ExtraWeaponSlot)), Times.Once);
    }

    [Theory]
    [InlineData("actor", 6, false, false)]
    [InlineData("actor", 8, false, false)]
    [InlineData("other", 7, false, false)]
    [InlineData("actor", 7, true, false)]
    [InlineData("actor", 7, false, true)]
    public void Receive_RejectsWrongAuthorityLocalOwnerAndOldMission(
        string authority, long revision, bool local, bool oldMission)
    {
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(local);
        if (oldMission) AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, New<Mission>());
        Receive(Message(authority, revision));
        Assert.Empty(Calls);
        Assert.True(agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
    }

    [Fact]
    public void LocalGrant_OnlyOwnerAnnouncesAndProtectsPendingDropReconciliation()
    {
        SetExtra(agent, new MissionWeapon(item, null, null, 0));
        grant(new MessagePayload<LadderForkGranted>(this, new LadderForkGranted(agent)));
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(true);
        grant(new MessagePayload<LadderForkGranted>(this, new LadderForkGranted(agent)));
        network.Verify(n => n.SendAll(It.Is<NetworkLadderForkGranted>(m =>
            m.AgentId == info.AgentId && m.AuthorityRevision == 7 && m.ItemObjectId == "fork-id")), Times.Once);
        broker.Verify(b => b.Publish(It.IsAny<object>(), It.Is<WeaponPickupApplied>(p =>
            p.AgentId == info.AgentId && p.WorldItemId == Guid.Empty && p.SlotTransitionApplied)), Times.Once);
    }

    [Fact]
    public void CatchUp_PacksCurrentExtraSlotRatherThanReplayingOldGrant()
    {
        var mapper = new MissionWeaponDataMapper(objects.Object);
        var replicator = New<OwnedAgentReplicator>();
        AccessTools.Field(typeof(OwnedAgentReplicator), "missionWeaponDataMapper").SetValue(replicator, mapper);
        var pack = AccessTools.Method(typeof(OwnedAgentReplicator), "PackMissionEquipmentData");
        SetExtra(agent, new MissionWeapon(item, null, null, 0));
        var held = (MissionEquipmentData)pack.Invoke(replicator, new object[] { agent.Equipment });
        Assert.Equal("fork-id", held.WeaponSlots[(int)EquipmentIndex.ExtraWeaponSlot].ItemObjectId);
        SetExtra(agent, default);
        var dropped = (MissionEquipmentData)pack.Invoke(replicator, new object[] { agent.Equipment });
        Assert.Null(dropped.WeaponSlots[(int)EquipmentIndex.ExtraWeaponSlot].ItemObjectId);
    }

    [Fact]
    public void Patch_OnlyCapturesTheCurrentLadderForkStandingPoint()
    {
        var ladder = New<SiegeLadder>();
        var point = New<StandingPointWithWeaponRequirement>();
        AccessTools.Field(typeof(SiegeLadder), "_forkPickUpStandingPoint").SetValue(ladder, point);
        AccessTools.Field(typeof(SiegeLadder), "_forkItem").SetValue(ladder, item);
        AccessTools.Property(typeof(Agent), "CurrentlyUsedGameObject").SetValue(agent, point);
        ((ICollection<MissionObject>)mission.Instance.MissionObjects).Add(ladder);
        Assert.True(LadderForkGrantPatch.IsSiegeEquipmentGrant(agent, new MissionWeapon(item, null, null, 0)));
        Assert.False(LadderForkGrantPatch.IsSiegeEquipmentGrant(agent, default));
        AccessTools.Property(typeof(Agent), "CurrentlyUsedGameObject").SetValue(agent, New<StandingPoint>());
        Assert.False(LadderForkGrantPatch.IsSiegeEquipmentGrant(agent, new MissionWeapon(item, null, null, 0)));
    }

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    public void Patch_BindsMangonelMissileToItsCurrentPickupPoint(
        bool wrongItem, bool wrongPoint, bool oldMission, bool missingMachine, bool expected)
    {
        var ammo = RegisterAmmo();
        var mangonel = New<Mangonel>();
        var point = New<StandingPointWithWeaponRequirement>();
        AccessTools.Field(typeof(RangedSiegeWeapon), "OriginalMissileItem").SetValue(mangonel, ammo);
        AccessTools.Property(typeof(UsableMachine), "AmmoPickUpPoints").SetValue(mangonel,
            new List<StandingPoint> { point });
        AccessTools.Property(typeof(Agent), "CurrentlyUsedGameObject").SetValue(agent,
            wrongPoint ? New<StandingPoint>() : point);
        if (!missingMachine) ((ICollection<MissionObject>)mission.Instance.MissionObjects).Add(mangonel);
        if (oldMission) AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, New<Mission>());
        Assert.Equal(expected, LadderForkGrantPatch.IsSiegeEquipmentGrant(agent,
            new MissionWeapon(wrongItem ? item : ammo, null, null, 1)));
        Assert.False(LadderForkGrantPatch.IsSiegeEquipmentGrant(agent, default));
    }

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    public void Patch_BindsStonePileRockToExactCurrentPickupPoint(
        bool wrongItem, bool wrongPoint, bool oldMission, bool foreignPile, bool expected)
    {
        var ammo = RegisterAmmo();
        var pile = New<StonePile>();
        var point = New<StandingPoint>();
        AccessTools.Field(typeof(StonePile), "_givenItem").SetValue(pile, ammo);
        AccessTools.Property(typeof(UsableMachine), "AmmoPickUpPoints").SetValue(pile,
            new List<StandingPoint> { point });
        AccessTools.Property(typeof(Agent), "CurrentlyUsedGameObject").SetValue(agent,
            wrongPoint ? New<StandingPoint>() : point);
        if (!foreignPile) ((ICollection<MissionObject>)mission.Instance.MissionObjects).Add(pile);
        if (oldMission) AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, New<Mission>());
        Assert.Equal(expected, LadderForkGrantPatch.IsSiegeEquipmentGrant(agent,
            new MissionWeapon(wrongItem ? item : ammo, null, null, 1)));
        Assert.False(LadderForkGrantPatch.IsSiegeEquipmentGrant(null, new MissionWeapon(ammo, null, null, 1)));
        Assert.False(LadderForkGrantPatch.IsSiegeEquipmentGrant(agent, default));
    }

    [Theory]
    [InlineData("actor", 7, false, false, true)]
    [InlineData("actor", 6, false, false, false)]
    [InlineData("actor", 8, false, false, false)]
    [InlineData("other", 7, false, false, false)]
    [InlineData("actor", 7, true, false, false)]
    [InlineData("actor", 7, false, true, false)]
    public void AmmoGrant_PreservesAmountAuthorityOrderingAndDuplicateProtection(
        string authority, long revision, bool local, bool oldMission, bool expected)
    {
        var ammo = RegisterAmmo();
        var weapon = new MissionWeapon(ammo, null, null, 1);
        var message = Serializer.DeepClone(new NetworkLadderForkGranted(Guid.NewGuid(), info.AgentId,
            authority, revision, "ammo-id", weapon.RawDataForNetwork,
            new AgentEquipmentData(EquipmentIndex.ExtraWeaponSlot, EquipmentIndex.None, 0)));
        Assert.Equal(1, new MissionWeapon(ammo, null, null, message.DataValue).Amount);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(local);
        if (oldMission) AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, New<Mission>());
        Receive(message);
        if (expected)
        {
            Assert.Same(ammo, agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Item);
            Assert.Equal(1, agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
            Assert.Equal(new[] { "equip", "wield" }, Calls);
            SetExtra(agent, default); // Loading or dropping the missile has consumed the grant.
            Receive(message);
            Assert.Equal(2, Calls.Count);
        }
        else Assert.Empty(Calls);
        Assert.True(agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
    }

    [Fact]
    public void AmmoGrant_OnlyOwnerAnnouncesAndCatchUpPacksCurrentMissile()
    {
        var ammo = RegisterAmmo();
        var weapon = new MissionWeapon(ammo, null, null, 1);
        SetExtra(agent, weapon);
        grant(new MessagePayload<LadderForkGranted>(this, new LadderForkGranted(agent)));
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(true);
        grant(new MessagePayload<LadderForkGranted>(this, new LadderForkGranted(agent)));
        network.Verify(n => n.SendAll(It.Is<NetworkLadderForkGranted>(m =>
            m.AgentId == info.AgentId && m.AuthorityRevision == 7 &&
            m.ItemObjectId == "ammo-id" && m.DataValue == weapon.RawDataForNetwork)), Times.Once);
        broker.Verify(b => b.Publish(It.IsAny<object>(), It.Is<WeaponPickupApplied>(p =>
            p.AgentId == info.AgentId && p.SlotTransitionApplied)), Times.Once);

        var replicator = New<OwnedAgentReplicator>();
        AccessTools.Field(typeof(OwnedAgentReplicator), "missionWeaponDataMapper")
            .SetValue(replicator, new MissionWeaponDataMapper(objects.Object));
        var pack = AccessTools.Method(typeof(OwnedAgentReplicator), "PackMissionEquipmentData");
        var held = (MissionEquipmentData)pack.Invoke(replicator, new object[] { agent.Equipment });
        Assert.Equal("ammo-id", held.WeaponSlots[(int)EquipmentIndex.ExtraWeaponSlot].ItemObjectId);
        SetExtra(agent, default);
        var loaded = (MissionEquipmentData)pack.Invoke(replicator, new object[] { agent.Equipment });
        Assert.Null(loaded.WeaponSlots[(int)EquipmentIndex.ExtraWeaponSlot].ItemObjectId);
    }

    [Fact]
    public void ConsumedGrant_ArrivingLateCannotResurrectAmmo()
    {
        var message = Message();
        Assert.False(info.ConsumeSiegeGrant(message.GrantId));
        Receive(message);
        Assert.True(agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.Empty(Calls);
        var newer = Message();
        Receive(newer);
        Assert.Equal(newer.GrantId, info.SiegeEquipmentGrant);
        Assert.False(info.ConsumeSiegeGrant(message.GrantId));
        Assert.Same(item, agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Item);
    }

    private ItemObject RegisterAmmo()
    {
        var ammo = new ItemObject("grapeshot_stack");
        ammo.AddWeapon(new WeaponComponentData(null, WeaponClass.Boulder, default), null);
        var registered = ammo;
        objects.Setup(o => o.TryGetObjectWithLogging("ammo-id", out registered)).Returns(true);
        string id = "ammo-id";
        objects.Setup(o => o.TryGetIdWithLogging(ammo, out id)).Returns(true);
        objects.Setup(o => o.TryGetId(ammo, out id)).Returns(true);
        return ammo;
    }

    private NetworkLadderForkGranted Message(string authority = "actor", long revision = 7) =>
        new(Guid.NewGuid(), info.AgentId, authority, revision, "fork-id", 0,
            new AgentEquipmentData(EquipmentIndex.ExtraWeaponSlot, EquipmentIndex.None, 0));

    private void Receive(NetworkLadderForkGranted message)
    {
        receive(new MessagePayload<NetworkLadderForkGranted>(this, message));
        GameThread.Run(() => { }, blocking: true);
    }

    private void Stub(Type type, string target, string prefix) => harmony.Patch(
        AccessTools.Method(type, target), prefix: new HarmonyMethod(
            AccessTools.Method(typeof(LadderForkReplicationTests), prefix)));

    private static bool Active(ref bool __result) { __result = true; return false; }
    private static bool MainHand(ref EquipmentIndex __result) { __result = EquipmentIndex.ExtraWeaponSlot; return false; }
    private static bool OffHand(ref EquipmentIndex __result) { __result = EquipmentIndex.None; return false; }
    private static bool Skip() => false;
    private static bool Equip(Agent __instance, ref MissionWeapon weapon)
    {
        Assert.True(AllowedThread.IsThisThreadAllowed());
        SetExtra(__instance, weapon);
        Calls.Add("equip");
        return false;
    }
    private static bool Wield()
    {
        Assert.True(AllowedThread.IsThisThreadAllowed());
        Calls.Add("wield");
        return false;
    }
    private static void SetExtra(Agent target, MissionWeapon weapon)
    {
        var slots = (MissionWeapon[])AccessTools.Field(typeof(MissionEquipment), "_weaponSlots").GetValue(target.Equipment);
        slots[(int)EquipmentIndex.ExtraWeaponSlot] = weapon;
    }
#pragma warning disable SYSLIB0050
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
    public void Dispose()
    {
        handler.Dispose();
        harmony.UnpatchAll(harmony.Id);
        mission.Dispose();
    }
}
