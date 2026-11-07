using System.Collections.Generic;
using System.Reflection;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

// #4492: the poll compares the stored equipment with the live agent instead of capturing it every frame.
public partial class ActionEquipmentSyncTests
{
    // Mutation name, and whether TryCapture would see a different loadout afterwards.
    public static IEnumerable<object[]> LiveLoadoutMutations => new[]
    {
        new object[] { "none", false },
        new object[] { "arrow-amount", true },
        new object[] { "arrow-amount-to-zero", true },
        new object[] { "arrow-modifier-added", true },
        new object[] { "arrow-modifier-removed", true },
        new object[] { "arrow-modifier-changed", true },
        new object[] { "arrow-item-swapped-same-amount", true },
        new object[] { "arrow-class-only", false },
        new object[] { "ammo-slot-added", true },
        new object[] { "ammo-slot-removed", true },
        new object[] { "ammo-stack-moved-slot", true },
        new object[] { "ammo-became-non-ammo", true },
        new object[] { "main-hand-wield", true },
        new object[] { "main-hand-unwield", true },
        new object[] { "off-hand-wield", true },
        new object[] { "shield-off-hand-unwield", true },
        new object[] { "shield-off-hand-item-swapped", true },
        new object[] { "shield-off-hand-unchanged-refresh", false },
        new object[] { "main-hand-usage", true },
        new object[] { "main-hand-item-swapped", true },
        new object[] { "slots-unsafe", true },
        new object[] { "extra-slot-ammo", false },
        new object[] { "extra-slot-weapon", false },
    };

    private const EquipmentIndex BowSlot = EquipmentIndex.Weapon0;
    private const EquipmentIndex ArrowSlot = EquipmentIndex.Weapon1;
    private const EquipmentIndex BoltSlot = EquipmentIndex.Weapon2;
    private const EquipmentIndex SpareSlot = EquipmentIndex.Weapon3;

    [Theory]
    [MemberData(nameof(LiveLoadoutMutations))]
    public void MatchesLive_AgreesWithTryCaptureAndEquals(string mutation, bool expectedChange)
    {
        RunScenario(context =>
        {
            Agent agent = context.Spawn("peer", out MirrorAgent mirror, out _);
            ArcherLoadout(mirror, mutation);
            Assert.True(AgentEquipmentData.TryCapture(agent, out AgentEquipmentData stored));
            Assert.True(stored.MatchesLive(agent));

            Mutate(mutation, mirror);

            Assert.True(AgentEquipmentData.TryCapture(agent, out AgentEquipmentData live));
            Assert.Equal(!expectedChange, stored.Equals(live));
            Assert.Equal(stored.Equals(live), stored.MatchesLive(agent));
        });
    }

    [Theory]
    [MemberData(nameof(LiveLoadoutMutations))]
    public void ActionPoll_PublishesEveryRealEquipmentChangeAndNothingElse(string mutation, bool expectedChange)
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out MirrorAgent mirror, out _);
            ArcherLoadout(mirror, mutation);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            Mutate(mutation, mirror);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            if (expectedChange)
            {
                var packet = Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
                Assert.Equal(2, Assert.Single(packet.Actions).EquipmentRevision);
            }
            else
            {
                Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            }

            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
        });
    }

    [Fact]
    public void ActionPoll_PublishedEquipment_IsTheLiveCaptureWithoutASecondRead()
    {
        RunScenario(context =>
        {
            Agent agent = context.Spawn("peer", out MirrorAgent mirror, out _);
            ArcherLoadout(mirror, "arrow-amount");
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();

            Mutate("arrow-amount", mirror);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            var action = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.Equal(new AgentEquipmentData(agent), action.Equipment.Value);
            Assert.Equal(28, action.Equipment.Value.ArrowAmmo[0].Amount);
        });
    }

    [Fact]
    public void MatchesLive_NonHumanAgent_IsFalse()
    {
        using var fixture = new MissionEngineFixture();
        var animal = ObjectHelper.SkipConstructor<Agent>();
        AgentMirror.Bind(animal, new MirrorAgent { IsHuman = false });

        Assert.False(default(AgentEquipmentData).MatchesLive(animal));
    }

    // A bow in hand with two ammo stacks. Mutations named "shield-" start with a shield wielded in the off hand.
    private static void ArcherLoadout(MirrorAgent mirror, string mutation)
    {
        mirror.Equipment[BowSlot] = Weapon("bow", 2);
        mirror.Equipment[ArrowSlot] = Ammo("arrows", 29);
        mirror.Equipment[BoltSlot] = Ammo("bolts", 10, "sharp", WeaponClass.Bolt);
        mirror.PrimaryWieldedItemIndex = BowSlot;
        mirror.OffhandWieldedItemIndex = EquipmentIndex.None;
        if (mutation.StartsWith("shield-"))
        {
            mirror.Equipment[SpareSlot] = Weapon("shield");
            mirror.OffhandWieldedItemIndex = SpareSlot;
        }
    }

    private static void Mutate(string mutation, MirrorAgent mirror)
    {
        MissionEquipment equipment = mirror.Equipment;
        switch (mutation)
        {
            case "none":
                break;
            case "arrow-amount":
                SetAmount(equipment, ArrowSlot, 28);
                break;
            case "arrow-amount-to-zero":
                SetAmount(equipment, ArrowSlot, 0);
                break;
            case "arrow-modifier-added":
                equipment[ArrowSlot] = Ammo("arrows", 29, "sharp");
                break;
            case "arrow-modifier-removed":
                equipment[BoltSlot] = Ammo("bolts", 10, null, WeaponClass.Bolt);
                break;
            case "arrow-modifier-changed":
                equipment[BoltSlot] = Ammo("bolts", 10, "heavy", WeaponClass.Bolt);
                break;
            case "arrow-item-swapped-same-amount":
                equipment[ArrowSlot] = Ammo("barbed_arrows", 29);
                break;
            case "arrow-class-only":
                // The weapon class decides whether a slot is ammo but is not sent, so same item and amount is no change.
                equipment[ArrowSlot] = Ammo("arrows", 29, null, WeaponClass.Bolt);
                break;
            case "ammo-slot-added":
                equipment[SpareSlot] = Ammo("more_arrows", 12);
                break;
            case "ammo-slot-removed":
                equipment[BoltSlot] = MissionWeapon.Invalid;
                break;
            case "ammo-stack-moved-slot":
                equipment[ArrowSlot] = MissionWeapon.Invalid;
                equipment[SpareSlot] = Ammo("arrows", 29);
                break;
            case "ammo-became-non-ammo":
                equipment[ArrowSlot] = Weapon("arrows");
                break;
            case "main-hand-wield":
                mirror.PrimaryWieldedItemIndex = ArrowSlot;
                break;
            case "main-hand-unwield":
                mirror.PrimaryWieldedItemIndex = EquipmentIndex.None;
                break;
            case "off-hand-wield":
                equipment[SpareSlot] = Weapon("shield");
                mirror.OffhandWieldedItemIndex = SpareSlot;
                break;
            case "main-hand-usage":
                SetUsage(equipment, BowSlot, 1);
                break;
            case "main-hand-item-swapped":
                equipment[BowSlot] = Weapon("long_bow", 2);
                break;
            case "shield-off-hand-unwield":
                mirror.OffhandWieldedItemIndex = EquipmentIndex.None;
                break;
            case "shield-off-hand-item-swapped":
                equipment[SpareSlot] = Weapon("buckler");
                break;
            case "shield-off-hand-unchanged-refresh":
                equipment[SpareSlot] = Weapon("shield");
                break;
            case "slots-unsafe":
                SetUsage(equipment, BowSlot, 5);
                break;
            case "extra-slot-ammo":
                equipment[EquipmentIndex.ExtraWeaponSlot] = Ammo("spare_arrows", 3);
                break;
            case "extra-slot-weapon":
                equipment[EquipmentIndex.ExtraWeaponSlot] = Weapon("buckler");
                break;
            default:
                throw new System.ArgumentException("unknown mutation " + mutation);
        }
    }

    private static void SetAmount(MissionEquipment equipment, EquipmentIndex slot, short amount)
    {
        MissionWeapon weapon = equipment[slot];
        weapon.Amount = amount;
        equipment[slot] = weapon;
    }

    private static void SetUsage(MissionEquipment equipment, EquipmentIndex slot, int usageIndex)
    {
        MissionWeapon weapon = equipment[slot];
        weapon.CurrentUsageIndex = usageIndex;
        equipment[slot] = weapon;
    }

    private static MissionWeapon Ammo(
        string itemId, short amount, string modifierId = null, WeaponClass weaponClass = WeaponClass.Arrow)
    {
        using var allowed = new AllowedThread();
        var item = new ItemObject { StringId = itemId };
        Assert.Equal(itemId, item.StringId);
        ItemModifier modifier = null;
        if (modifierId != null)
        {
            modifier = new ItemModifier { StringId = modifierId };
            Assert.Equal(modifierId, modifier.StringId);
        }

        object boxed = new MissionWeapon(item, modifier, null);
        typeof(MissionWeapon).GetField("_weapons", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(boxed, new List<WeaponComponentData> { new WeaponComponentData(null, weaponClass, default) });
        typeof(MissionWeapon).GetField("_modifiedMaxDataValue", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(boxed, (short)30);
        var weapon = (MissionWeapon)boxed;
        weapon.Amount = amount;
        return weapon;
    }
}
