using Common;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using GameInterface;
using GameInterface.Surrogates;
using HarmonyLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Agents.Patches;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using ProtoBuf;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

[Collection(nameof(Tournaments.TournamentCombatInfrastructureCollection))]
public class WeaponPickupSyncTests
{
    private static readonly List<string> ApplyCalls = new();
    private static EquipmentIndex pickedItemSlot;
    private static AgentEquipmentData appliedEquipment;
    private static bool pickupRanInsideAllowedThread;
    private static bool runtimeEquipmentRanInsideAllowedThread;
    private static bool wieldRanInsideAllowedThread;

    [Fact]
    public void ReplicatedPickup_RoundTripsCompleteMessage()
    {
        new SurrogateCollection();

        var equipment = new AgentEquipmentData(
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon2,
            0);
        var itemModifier = new ItemModifier
        {
            Damage = 10,
        };
        var banner = new Banner();
        var agentId = System.Guid.NewGuid();
        var worldItemId = System.Guid.NewGuid();
        var message = new NetworkWeaponPickedup(
            agentId,
            EquipmentIndex.Weapon2,
            worldItemId,
            "ItemObject_test_weapon",
            itemModifier,
            banner,
            equipment);
        PropertyInfo? property = typeof(NetworkWeaponPickedup).GetProperty(
            nameof(NetworkWeaponPickedup.CurrentEquipment));
        Assert.NotNull(property);
        ProtoMemberAttribute? member = property.GetCustomAttribute<ProtoMemberAttribute>();
        Assert.NotNull(member);
        PropertyInfo? worldItemProperty = typeof(NetworkWeaponPickedup).GetProperty(
            nameof(NetworkWeaponPickedup.WorldItemId));
        Assert.NotNull(worldItemProperty);
        ProtoMemberAttribute? worldItemMember =
            worldItemProperty.GetCustomAttribute<ProtoMemberAttribute>();
        Assert.NotNull(worldItemMember);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(message, serializer);
        var received = Assert.IsType<NetworkWeaponPickedup>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(6, member.Tag);
        Assert.Equal(7, worldItemMember.Tag);
        Assert.Equal(agentId, received.AgentId);
        Assert.Equal(EquipmentIndex.Weapon2, received.EquipmentIndex);
        Assert.Equal(worldItemId, received.WorldItemId);
        Assert.Equal("ItemObject_test_weapon", received.ItemObjectId);
        Assert.Equal(itemModifier.Damage, received.ItemModifier.Damage);
        Assert.Equal(banner.Serialize(), received.Banner.Serialize());
        Assert.Equal((int)EquipmentIndex.Weapon0, received.CurrentEquipment.MainHandIndex);
        Assert.Equal((int)EquipmentIndex.Weapon2, received.CurrentEquipment.OffHandIndex);
        Assert.Equal(0, received.CurrentEquipment.MainHandUsageIndex);
    }

    [Fact]
    public void MissionModule_RegistersWeaponDropPatchCategory()
    {
        HarmonyPatchCategoryRegistration registration = Assert.Single(
            MissionModule.CreatePatchCategoryRegistrations(),
            candidate => candidate.Category == MissionModule.WeaponDropPatchCategory);
        var harmony = new Harmony(
            $"{nameof(MissionModule_RegistersWeaponDropPatchCategory)}.{System.Guid.NewGuid()}");
        MethodInfo target = AccessTools.Method(
            typeof(Agent),
            nameof(Agent.DropItem),
            new[] { typeof(EquipmentIndex), typeof(WeaponClass) });

        try
        {
            registration.Apply(harmony);

            Patches patches = Harmony.GetPatchInfo(target);
            Patch patch = Assert.Single(
                patches.Postfixes,
                candidate => candidate.owner == harmony.Id);
            Assert.Equal(typeof(AgentDropPatch), patch.PatchMethod.DeclaringType);
        }
        finally
        {
            harmony.Unpatch(
                target,
                HarmonyPatchType.All,
                harmony.Id);
        }
    }

    [Fact]
    public void MissionModule_RegistersWeaponPickupPatchCategory()
    {
        HarmonyPatchCategoryRegistration registration = Assert.Single(
            MissionModule.CreatePatchCategoryRegistrations(),
            candidate => candidate.Category == MissionModule.WeaponPickupPatchCategory);
        var harmony = new Harmony(
            $"{nameof(MissionModule_RegistersWeaponPickupPatchCategory)}.{System.Guid.NewGuid()}");
        MethodInfo target = AccessTools.Method(typeof(Agent), "OnItemPickup");

        try
        {
            registration.Apply(harmony);

            Patches patches = Harmony.GetPatchInfo(target);
            Assert.Contains(
                patches.Postfixes,
                patch => patch.owner == harmony.Id);
        }
        finally
        {
            harmony.Unpatch(
                target,
                HarmonyPatchType.All,
                harmony.Id);
        }
    }

    [Fact]
    public void ApplyWeaponPickup_UsesWorldItemBeforeApplyingWieldState()
    {
        var harmony = new Harmony($"{nameof(ApplyWeaponPickup_UsesWorldItemBeforeApplyingWieldState)}.{System.Guid.NewGuid()}");
        MethodInfo pickup = AccessTools.Method(
            typeof(WeaponPickupHandler),
            "ApplyWorldItemPickup");
        MethodInfo wield = AccessTools.Method(
            typeof(AgentEquipmentData),
            nameof(AgentEquipmentData.Apply),
            new[] { typeof(Agent) });

        ApplyCalls.Clear();
        pickedItemSlot = EquipmentIndex.None;
        pickupRanInsideAllowedThread = false;
        wieldRanInsideAllowedThread = false;

        try
        {
            harmony.Patch(
                AccessTools.Method(
                    typeof(ScriptComponentBehavior),
                    "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(SkipScriptComponentCache))));
            harmony.Patch(
                pickup,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CapturePickup))));
            harmony.Patch(
                wield,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureWield))));

            Agent agent = ObjectHelper.SkipConstructor<Agent>();
            SpawnedItemEntity worldItem = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
            CoopAgentInfo agentInfo = CreateAgentInfo(agent);
            MissionWeapon weapon = default;
            var equipment = new AgentEquipmentData(
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon2,
                0);

            WeaponPickupHandler.ApplyWeaponPickup(
                agentInfo,
                worldItem,
                EquipmentIndex.Weapon2,
                ref weapon,
                equipment);

            Assert.Equal(new[] { "pickup", "wield" }, ApplyCalls);
            Assert.Equal(EquipmentIndex.Weapon2, pickedItemSlot);
            Assert.True(agentInfo.TryGetAuthoritativeEquipment(out AgentEquipmentData recorded));
            Assert.Equal(equipment, recorded);
            Assert.True(pickupRanInsideAllowedThread);
            Assert.True(wieldRanInsideAllowedThread);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ApplyWeaponPickup_UsesDetachedWeaponWithoutStableWorldItemIdentity()
    {
        var harmony = new Harmony($"{nameof(ApplyWeaponPickup_UsesDetachedWeaponWithoutStableWorldItemIdentity)}.{System.Guid.NewGuid()}");
        MethodInfo pickup = AccessTools.Method(
            typeof(WeaponPickupHandler),
            "ApplyDetachedWeaponPickup");
        MethodInfo wield = AccessTools.Method(
            typeof(AgentEquipmentData),
            nameof(AgentEquipmentData.Apply),
            new[] { typeof(Agent) });

        ApplyCalls.Clear();
        pickedItemSlot = EquipmentIndex.None;
        pickupRanInsideAllowedThread = false;
        wieldRanInsideAllowedThread = false;

        try
        {
            harmony.Patch(
                pickup,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureDetachedPickup))));
            harmony.Patch(
                wield,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureWield))));

            Agent agent = ObjectHelper.SkipConstructor<Agent>();
            CoopAgentInfo agentInfo = CreateAgentInfo(agent);
            MissionWeapon weapon = default;
            var equipment = new AgentEquipmentData(
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon2,
                0);

            WeaponPickupHandler.ApplyWeaponPickup(
                agentInfo,
                null,
                EquipmentIndex.Weapon2,
                ref weapon,
                equipment);

            Assert.Equal(new[] { "pickup", "wield" }, ApplyCalls);
            Assert.Equal(EquipmentIndex.Weapon2, pickedItemSlot);
            Assert.True(agentInfo.TryGetAuthoritativeEquipment(out AgentEquipmentData recorded));
            Assert.Equal(equipment, recorded);
            Assert.True(pickupRanInsideAllowedThread);
            Assert.True(wieldRanInsideAllowedThread);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void TournamentRuntimeEquipment_AppliesWieldStateAfterSlotReconciliation()
    {
        var equipment = new AgentEquipmentData(
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon2,
            0);

        AssertRuntimeEquipmentApply(
            equipment,
            authoritativeEquipment: null,
            expectedEquipment: equipment);
    }

    [Fact]
    public void TournamentRuntimeEquipment_DoesNotOverwriteNewerAuthoritativeWieldState()
    {
        var staleRuntimeEquipment = new AgentEquipmentData(
            EquipmentIndex.Weapon0,
            EquipmentIndex.None,
            0);
        var authoritativeEquipment = new AgentEquipmentData(
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon2,
            0);

        AssertRuntimeEquipmentApply(
            staleRuntimeEquipment,
            authoritativeEquipment,
            authoritativeEquipment);
    }

    private static void AssertRuntimeEquipmentApply(
        AgentEquipmentData runtimeEquipment,
        AgentEquipmentData? authoritativeEquipment,
        AgentEquipmentData expectedEquipment)
    {
        var harmony = new Harmony($"{nameof(AssertRuntimeEquipmentApply)}.{System.Guid.NewGuid()}");
        MethodInfo reconcile = AccessTools.Method(
            typeof(CoopTournamentController),
            "ReconcileRuntimeEquipment");
        MethodInfo wield = AccessTools.Method(
            typeof(AgentEquipmentData),
            nameof(AgentEquipmentData.Apply),
            new[] { typeof(Agent) });

        ApplyCalls.Clear();
        appliedEquipment = default;
        runtimeEquipmentRanInsideAllowedThread = false;
        wieldRanInsideAllowedThread = false;

        try
        {
            harmony.Patch(
                reconcile,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureRuntimeEquipment))));
            harmony.Patch(
                wield,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureWield))));

            var runtime = new TournamentAgentRuntimeData(
                System.Guid.NewGuid(),
                100f,
                System.Array.Empty<TournamentMissionWeaponData>(),
                runtimeEquipment);
            CoopTournamentController controller =
                ObjectHelper.SkipConstructor<CoopTournamentController>();
            CoopAgentInfo agentInfo = CreateAgentInfo(
                ObjectHelper.SkipConstructor<Agent>());
            if (authoritativeEquipment.HasValue)
                agentInfo.RecordAuthoritativeEquipment(authoritativeEquipment.Value);

            controller.ReconcileRuntimeAgentEquipment(agentInfo, runtime);

            Assert.Equal(new[] { "slots", "wield" }, ApplyCalls);
            Assert.Equal(expectedEquipment, appliedEquipment);
            Assert.True(runtimeEquipmentRanInsideAllowedThread);
            Assert.True(wieldRanInsideAllowedThread);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static CoopAgentInfo CreateAgentInfo(Agent agent)
    {
        return new CoopAgentInfo(
            "owner",
            "owner",
            "owner",
            agent,
            System.Guid.NewGuid(),
            1);
    }

    private static bool CapturePickup(bool isSuccessful, int preferenceIndex)
    {
        ApplyCalls.Add("pickup");
        Assert.True(isSuccessful);
        pickedItemSlot = (EquipmentIndex)preferenceIndex;
        pickupRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }

    private static bool CaptureDetachedPickup(EquipmentIndex equipmentIndex)
    {
        ApplyCalls.Add("pickup");
        pickedItemSlot = equipmentIndex;
        pickupRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }

    private static bool SkipScriptComponentCache() => false;

    private static bool CaptureRuntimeEquipment()
    {
        ApplyCalls.Add("slots");
        runtimeEquipmentRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }

    private static bool CaptureWield(ref AgentEquipmentData __instance)
    {
        ApplyCalls.Add("wield");
        appliedEquipment = __instance;
        wieldRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }
}
