using System.Collections.Generic;
using System.Reflection;
using Common.PacketHandlers;
using Common.Serialization;
#if DEBUG
using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Battles;
using Moq;
using TaleWorlds.MountAndBlade.View.Screens;
#endif
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public partial class ActionEquipmentSyncTests
{
    [Fact]
    public void ArrowRefillWithoutActionOrWieldChange_PublishesEquipmentRevision()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("bow");
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(29);
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();

            var arrows = mirror.Equipment[EquipmentIndex.Weapon1];
            arrows.Amount = 30;
            mirror.Equipment[EquipmentIndex.Weapon1] = arrows;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            var packet = Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(2, Assert.Single(packet.Actions).EquipmentRevision);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
        });
    }

    [Theory]
    [InlineData(WeaponClass.Arrow, 0)]
    [InlineData(WeaponClass.Arrow, 30)]
    [InlineData(WeaponClass.Bolt, 30)]
    public void ArrowAmmoRoundTrip_AppliesBeforeActionAndIgnoresDuplicate(WeaponClass weaponClass, short amount)
    {
        RunScenario(context =>
        {
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            context.Spawn("owner", out var puppet, out var id);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(amount, weaponClass);
            puppet.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(5, weaponClass);
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var packet = Assert.IsType<AgentActionPacket>(serializer.Deserialize<IPacket>(
                serializer.Serialize(Packet(owner, id, 1))));
            Assert.Equal(new AgentEquipmentData(owner), packet.Actions[0].Equipment.Value);

            context.Receive(packet);

            Assert.Equal(amount, puppet.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.Equal(1001, puppet.Action0Index);
            Assert.True(puppet.ActionAndGuardCallOrder.IndexOf("ammo") >= 0);
            Assert.True(puppet.ActionAndGuardCallOrder.IndexOf("ammo") < puppet.ActionAndGuardCallOrder.IndexOf("set-action"));
            int calls = puppet.ActionAndGuardCallOrder.Count;
            context.Receive(packet);
            Assert.Equal(calls, puppet.ActionAndGuardCallOrder.Count);
        });
    }

    [Fact]
    public void ArrowAmmoCatchUp_PreservesRefillForExistingPeers()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(29);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(30);
            context.Component.AgentActionHandler.CatchUpJoiner("joiner");
            Drain();
            var catchUp = Assert.Single(Assert.IsType<AgentActionPacket>(
                Assert.Single(context.Network.DirectPacketSends).Packet).Actions);
            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            var broadcast = Assert.Single(Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);

            Assert.Equal(30, Assert.Single(catchUp.Equipment.Value.ArrowAmmo).Amount);
            Assert.Equal(catchUp.EquipmentRevision, broadcast.EquipmentRevision);
            Assert.Equal(catchUp.Equipment, broadcast.Equipment);
        });
    }

    [Fact]
    public void ArrowAmmoOldAuthorityOrRevision_CannotReplaceCurrentRefill()
    {
        RunScenario(context =>
        {
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            context.Spawn("owner", out var puppet, out var id);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(29);
            puppet.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(5);
            var old = RevisionPacket(owner, id, 1, 1, true);
            context.Receive(old);
            Assert.Equal(29, puppet.Equipment[EquipmentIndex.Weapon1].Amount);

            ownerMirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(30);
            context.Receive(RevisionPacket(owner, id, 2, 2, true));
            context.Receive(old);
            Assert.Equal(30, puppet.Equipment[EquipmentIndex.Weapon1].Amount);

            Assert.True(context.Registry.TryTransferAuthority("next-owner", id));
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(12);
            context.Receive(RevisionPacket(owner, id, 3, 3, true));
            Assert.Equal(30, puppet.Equipment[EquipmentIndex.Weapon1].Amount);
            context.Receive(RevisionPacket(owner, id, 4, 1, true, "next-owner", authorityRevision: 1));
            Assert.Equal(12, puppet.Equipment[EquipmentIndex.Weapon1].Amount);
        });
    }

    [Theory]
    [InlineData(-1, "arrows", null, 30)]
    [InlineData(4, "arrows", null, 30)]
    [InlineData(1, "other_item", null, 30)]
    [InlineData(1, "arrows", "other_modifier", 30)]
    [InlineData(1, "arrows", null, -1)]
    [InlineData(1, "arrows", null, 31)]
    public void ArrowAmmoInvalidIdentityOrAmount_DoesNotMutate(int slot, string itemId, string modifierId, short amount)
    {
        RunScenario(context =>
        {
            var puppet = context.Spawn("owner", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(5);
            var data = new AgentEquipmentData(EquipmentIndex.None, EquipmentIndex.None, 0,
                arrowAmmo: new[] { new AgentArrowAmmoData(slot, itemId, modifierId, amount) });

            Assert.False(data.TryApplyForAction(puppet));
            Assert.Equal(5, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.DoesNotContain("ammo", mirror.ActionAndGuardCallOrder);
        });
    }

    [Fact]
    public void ArrowAmmoDuplicateOrIncompleteSlots_DoNotPartiallyMutate()
    {
        RunScenario(context =>
        {
            var puppet = context.Spawn("owner", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(5);
            mirror.Equipment[EquipmentIndex.Weapon2] = ArrowWeapon(5);
            var ammo = new AgentArrowAmmoData(1, "arrows", null, 30);
            var incomplete = new AgentEquipmentData(EquipmentIndex.None, EquipmentIndex.None, 0, arrowAmmo: new[] { ammo });
            var duplicate = new AgentEquipmentData(EquipmentIndex.None, EquipmentIndex.None, 0, arrowAmmo: new[] { ammo, ammo });

            Assert.False(incomplete.TryApplyForAction(puppet));
            Assert.False(duplicate.TryApplyForAction(puppet));
            Assert.Equal(5, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.DoesNotContain("ammo", mirror.ActionAndGuardCallOrder);
        });
    }

    [Fact]
    public void ArrowAmmoDoesNotCaptureExtraSlotOrModifyItThroughLegacyApply()
    {
        RunScenario(context =>
        {
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            var puppet = context.Spawn("owner", out var puppetMirror, out _);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(30);
            ownerMirror.Equipment[EquipmentIndex.ExtraWeaponSlot] = ArrowWeapon(1);
            puppetMirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(5);
            puppetMirror.Equipment[EquipmentIndex.ExtraWeaponSlot] = ArrowWeapon(7);
            var data = new AgentEquipmentData(owner);

            Assert.Equal(1, Assert.Single(data.ArrowAmmo).Slot);
            data.Apply(puppet);
            Assert.Equal(5, puppetMirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.True(data.TryApplyForAction(puppet));
            Assert.Equal(30, puppetMirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.Equal(7, puppetMirror.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        });
    }

#if DEBUG
    [Fact]
    public void ArrowFixture_DeficitIsReversibleAndCannotBePreparedTwice()
    {
        RunScenario(context =>
        {
            var agent = context.Spawn("peer", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(30);
            var behavior = ArrowFixture(context, agent, out var screen);

            behavior.PrepareArrowAmmo(screen, agent);
            Assert.Equal(29, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.Equal("fixture_arrow_deficit_prepared", AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
            behavior.PrepareArrowAmmo(screen, agent);
            Assert.Equal(29, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.Equal("fixture_arrow_prepare_rejected", AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
            Assert.True(behavior.RestoreArrowAmmo(agent));
            Assert.Equal(30, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "pressInvoked").GetValue(behavior));
            Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "externalInputArmed").GetValue(behavior));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArrowFixture_RejectsMissingCaptureOrAmbiguousAmmo(bool captured)
    {
        RunScenario(context =>
        {
            var agent = context.Spawn("peer", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(30);
            var behavior = ArrowFixture(context, agent, out var screen);
            if (captured) mirror.Equipment[EquipmentIndex.Weapon2] = ArrowWeapon(30);
            else AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").SetValue(behavior, null);

            behavior.PrepareArrowAmmo(screen, agent);

            Assert.Equal(30, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
            Assert.Equal("fixture_arrow_prepare_rejected", AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
            Assert.DoesNotContain("ammo", mirror.ActionAndGuardCallOrder);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArrowFixture_RestorationRefusesChangedItemOrUnownedAmount(bool changedItem)
    {
        RunScenario(context =>
        {
            var agent = context.Spawn("peer", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(30);
            var behavior = ArrowFixture(context, agent, out var screen);
            behavior.PrepareArrowAmmo(screen, agent);
            if (changedItem) mirror.Equipment[EquipmentIndex.Weapon1] = ArrowWeapon(29);
            else
            {
                var weapon = mirror.Equipment[EquipmentIndex.Weapon1];
                weapon.Amount = 10;
                mirror.Equipment[EquipmentIndex.Weapon1] = weapon;
            }
            int before = mirror.ActionAndGuardCallOrder.Count;

            Assert.False(behavior.RestoreArrowAmmo(agent));
            Assert.Equal(before, mirror.ActionAndGuardCallOrder.Count);
            Assert.Equal(changedItem ? 29 : 10, mirror.Equipment[EquipmentIndex.Weapon1].Amount);
        });
    }

    private static SiegeInteractionDebugBehavior ArrowFixture(Context context, Agent agent, out MissionScreen screen)
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        screen = ObjectHelper.SkipConstructor<MissionScreen>();
        context.Mission.MainAgent = agent;
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, context.Mission.Shell);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").SetValue(behavior, agent);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedScreen").SetValue(behavior, screen);
        return behavior;
    }
#endif

    private static MissionWeapon ArrowWeapon(short amount, WeaponClass weaponClass = WeaponClass.Arrow)
    {
        var weapon = Weapon("arrows");
        object boxed = weapon;
        typeof(MissionWeapon).GetField("_weapons", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(boxed, new List<WeaponComponentData>
            {
                new WeaponComponentData(null, weaponClass, default),
            });
        typeof(MissionWeapon).GetField("_modifiedMaxDataValue", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(boxed, (short)30);
        weapon = (MissionWeapon)boxed;
        weapon.Amount = amount;
        return weapon;
    }
}
