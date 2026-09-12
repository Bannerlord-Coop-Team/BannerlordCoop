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
using System;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly List<short> AppliedSlotAmounts = new();
    private static EquipmentIndex pickedItemSlot;
    private static AgentEquipmentData appliedEquipment;
    private static short worldItemAmountAtPickup;
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
        var pickupId = System.Guid.NewGuid();
        var message = new NetworkWeaponPickedup(
            agentId,
            EquipmentIndex.Weapon2,
            worldItemId,
            "ItemObject_test_weapon",
            itemModifier,
            banner,
            equipment,
            previousSlotAmount: 3,
            previousWorldItemAmount: 9,
            resultingSlotAmount: 7,
            resultingWorldItemAmount: 5,
            worldItemConsumed: false,
            resultingSlotItemObjectId: "ItemObject_existing_ammo",
            resultingSlotItemModifierId: "ItemModifier_test",
            resultingSlotBanner: banner,
            resultingSlotDataValue: 7,
            worldItemDataValue: 11,
            hasWorldItemDataValue: true,
            worldItemModifierId: "ItemModifier_world_item",
            pickupId: pickupId);
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
        Assert.Equal(3, received.PreviousSlotAmount);
        Assert.Equal(9, received.PreviousWorldItemAmount);
        Assert.Equal(7, received.ResultingSlotAmount);
        Assert.Equal(5, received.ResultingWorldItemAmount);
        Assert.False(received.WorldItemConsumed);
        Assert.Equal("ItemObject_existing_ammo", received.ResultingSlotItemObjectId);
        Assert.Equal("ItemModifier_test", received.ResultingSlotItemModifierId);
        Assert.Equal(banner.Serialize(), received.ResultingSlotBanner.Serialize());
        Assert.Equal(7, received.ResultingSlotDataValue);
        Assert.Equal(11, received.WorldItemDataValue);
        Assert.True(received.HasWorldItemDataValue);
        Assert.Equal("ItemModifier_world_item", received.WorldItemModifierId);
        Assert.Equal(pickupId, received.PickupId);
    }

    [Fact]
    public void WeaponDropResyncRequest_RoundTripsWorldItemAndAgents()
    {
        Guid worldItemId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        Guid[] agentIds = { Guid.NewGuid(), Guid.NewGuid() };
        Guid[] pickupIds = { Guid.NewGuid(), Guid.NewGuid() };
        var message = new NetworkWeaponDropResyncRequest(
            worldItemId,
            "requester",
            agentIds,
            new[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon2 },
            requestId,
            pickupIds);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        MessagePacket packet = MessagePacket.Create(message, serializer);
        var received = Assert.IsType<NetworkWeaponDropResyncRequest>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(worldItemId, received.WorldItemId);
        Assert.Equal("requester", received.RequesterControllerId);
        Assert.Equal(requestId, received.RequestId);
        Assert.Equal(agentIds, received.AgentIds);
        Assert.Equal(pickupIds, received.RequiredPickupIds);
        Assert.Equal(
            new[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon2 },
            received.EquipmentIndices);
    }

    [Fact]
    public void WeaponPickupSlotState_RoundTripsRequestAndRevision()
    {
        Guid requestId = Guid.NewGuid();
        Guid worldItemId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        var message = new NetworkWeaponPickupSlotState(
            agentId,
            EquipmentIndex.Weapon2,
            "ItemObject_test_weapon",
            "ItemModifier_test",
            "banner",
            dataValue: 7,
            equipment: new AgentEquipmentData(EquipmentIndex.Weapon2, EquipmentIndex.None, 0),
            requestId: requestId,
            worldItemId: worldItemId,
            stateRevision: 12,
            responderControllerId: "picker-owner");
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        MessagePacket packet = MessagePacket.Create(message, serializer);
        var received = Assert.IsType<NetworkWeaponPickupSlotState>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(requestId, received.RequestId);
        Assert.Equal(worldItemId, received.WorldItemId);
        Assert.Equal(agentId, received.AgentId);
        Assert.Equal(12, received.StateRevision);
        Assert.Equal("picker-owner", received.ResponderControllerId);
        Assert.Equal("ItemObject_test_weapon", received.ItemObjectId);
    }

    [Fact]
    public void ResyncRequestState_ChunksTargetsAndPickupIdsToResponderLimits()
    {
        Type stateType = typeof(WeaponPickupHandler).GetNestedType(
            "ResyncRequestState",
            BindingFlags.NonPublic);
        Assert.NotNull(stateType);
        object state = Activator.CreateInstance(
            stateType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { Guid.NewGuid(), "observer" },
            culture: null);
        Assert.NotNull(state);

        var targets = new HashSet<(Guid AgentId, EquipmentIndex Slot)>();
        for (int i = 0; i < 65; i++)
            targets.Add((Guid.NewGuid(), EquipmentIndex.Weapon0));
        var pickupIds = new HashSet<Guid>();
        for (int i = 0; i < 513; i++)
            pickupIds.Add(Guid.NewGuid());

        MethodInfo merge = stateType.GetMethod(
            "Merge",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo createRequests = stateType.GetMethod(
            "CreateRequests",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(merge);
        Assert.NotNull(createRequests);
        merge.Invoke(state, new object[] { targets, pickupIds });

        var requests = new List<NetworkWeaponDropResyncRequest>();
        foreach (object request in Assert.IsAssignableFrom<System.Collections.IEnumerable>(
                     createRequests.Invoke(state, new object[] { false })))
        {
            requests.Add(Assert.IsType<NetworkWeaponDropResyncRequest>(request));
        }

        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.InRange(request.AgentIds.Length, 0, 64));
        Assert.All(
            requests,
            request => Assert.InRange(request.RequiredPickupIds.Length, 0, 512));
        Assert.True(
            targets.Select(target => target.AgentId)
                .ToHashSet()
                .SetEquals(requests.SelectMany(request => request.AgentIds)));
        Assert.True(
            pickupIds.SetEquals(
                requests.SelectMany(request => request.RequiredPickupIds)));
    }

    [Fact]
    public void WeaponDropStateResponse_RoundTripsConsumedRevision()
    {
        Guid requestId = Guid.NewGuid();
        Guid worldItemId = Guid.NewGuid();
        Guid[] pickupIds = { Guid.NewGuid(), Guid.NewGuid() };
        var message = new NetworkWeaponDropStateResponse(
            requestId,
            worldItemId,
            stateRevision: 8,
            worldItemConsumed: true,
            drop: null,
            includedPickupIds: pickupIds,
            hasRemainingAmount: true,
            remainingAmount: 4);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        MessagePacket packet = MessagePacket.Create(message, serializer);
        var received = Assert.IsType<NetworkWeaponDropStateResponse>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(requestId, received.RequestId);
        Assert.Equal(worldItemId, received.WorldItemId);
        Assert.Equal(8, received.StateRevision);
        Assert.True(received.WorldItemConsumed);
        Assert.Null(received.Drop);
        Assert.Equal(pickupIds, received.IncludedPickupIds);
        Assert.True(received.HasRemainingAmount);
        Assert.Equal(4, received.RemainingAmount);
    }

    [Fact]
    public void EqualRevisionWorldResponses_MergeAllPickupReceiptChunks()
    {
        Type stateType = typeof(WeaponPickupHandler).GetNestedType(
            "ResyncRequestState",
            BindingFlags.NonPublic);
        Assert.NotNull(stateType);
        Guid worldItemId = Guid.NewGuid();
        object state = Activator.CreateInstance(
            stateType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { worldItemId, "observer" },
            culture: null);
        Assert.NotNull(state);
        Guid requestId = Assert.IsType<Guid>(
            stateType.GetProperty("RequestId").GetValue(state));
        Guid[] firstChunk = Enumerable.Range(0, 512).Select(_ => Guid.NewGuid()).ToArray();
        Guid[] secondChunk = { Guid.NewGuid() };
        MethodInfo merge = stateType.GetMethod(
            "MergeWorldResponse",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(merge);

        Assert.True((bool)merge.Invoke(
            state,
            new object[]
            {
                new NetworkWeaponDropStateResponse(
                    requestId,
                    worldItemId,
                    stateRevision: 7,
                    worldItemConsumed: true,
                    drop: null,
                    includedPickupIds: firstChunk),
            }));
        Assert.True((bool)merge.Invoke(
            state,
            new object[]
            {
                new NetworkWeaponDropStateResponse(
                    requestId,
                    worldItemId,
                    stateRevision: 7,
                    worldItemConsumed: true,
                    drop: null,
                    includedPickupIds: secondChunk),
            }));

        var merged = Assert.IsType<NetworkWeaponDropStateResponse>(
            stateType.GetProperty("PendingWorldResponse").GetValue(state));
        Assert.Equal(513, merged.IncludedPickupIds.Length);
        Assert.True(
            firstChunk.Concat(secondChunk).ToHashSet().SetEquals(merged.IncludedPickupIds));
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
    public void ApplyWeaponPickup_ReconcilesAmountsAroundWorldItemPickupBeforeApplyingWieldState()
    {
        var harmony = new Harmony($"{nameof(ApplyWeaponPickup_ReconcilesAmountsAroundWorldItemPickupBeforeApplyingWieldState)}.{System.Guid.NewGuid()}");
        MethodInfo pickup = AccessTools.Method(
            typeof(WeaponPickupHandler),
            "ApplyWorldItemPickup");
        MethodInfo setAmount = AccessTools.Method(
            typeof(Agent),
            nameof(Agent.SetWeaponAmountInSlot));
        MethodInfo wield = AccessTools.Method(
            typeof(AgentEquipmentData),
            nameof(AgentEquipmentData.Apply),
            new[] { typeof(Agent) });

        ApplyCalls.Clear();
        AppliedSlotAmounts.Clear();
        pickedItemSlot = EquipmentIndex.None;
        worldItemAmountAtPickup = 0;
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
                setAmount,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureSlotAmount))));
            harmony.Patch(
                wield,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureWield))));

            Agent agent = ObjectHelper.SkipConstructor<Agent>();
            InitializeEquipmentSlot(agent, EquipmentIndex.Weapon2);
            SpawnedItemEntity worldItem = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
            CoopAgentInfo agentInfo = CreateAgentInfo(agent);
            MissionWeapon weapon = default;
            MissionWeapon resultingWeapon = agent.Equipment[EquipmentIndex.Weapon2];
            var equipment = new AgentEquipmentData(
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon2,
                0);

            WeaponPickupHandler.ApplyWeaponPickup(
                agentInfo,
                worldItem,
                EquipmentIndex.Weapon2,
                ref weapon,
                equipment,
                previousSlotAmount: 0,
                previousWorldItemAmount: 9,
                resultingSlotAmount: 7,
                resultingWorldItemAmount: 5,
                worldItemConsumed: true,
                resultingSlotWeapon: ref resultingWeapon);

            Assert.Equal(new[] { "pickup", "wield" }, ApplyCalls);
            Assert.Equal(new short[] { 0, 7 }, AppliedSlotAmounts);
            Assert.Equal(EquipmentIndex.Weapon2, pickedItemSlot);
            Assert.Equal(9, worldItemAmountAtPickup);
            Assert.Equal(5, worldItem.WeaponCopy.Amount);
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
        MethodInfo setAmount = AccessTools.Method(
            typeof(Agent),
            nameof(Agent.SetWeaponAmountInSlot));

        ApplyCalls.Clear();
        AppliedSlotAmounts.Clear();
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
                setAmount,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureSlotAmount))));
            harmony.Patch(
                wield,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureWield))));

            Agent agent = ObjectHelper.SkipConstructor<Agent>();
            InitializeEquipmentSlot(agent, EquipmentIndex.Weapon2);
            CoopAgentInfo agentInfo = CreateAgentInfo(agent);
            MissionWeapon weapon = default;
            MissionWeapon resultingWeapon = agent.Equipment[EquipmentIndex.Weapon2];
            var equipment = new AgentEquipmentData(
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon2,
                0);

            WeaponPickupHandler.ApplyWeaponPickup(
                agentInfo,
                null,
                EquipmentIndex.Weapon2,
                ref weapon,
                equipment,
                previousSlotAmount: 3,
                previousWorldItemAmount: 9,
                resultingSlotAmount: 7,
                resultingWorldItemAmount: 5,
                worldItemConsumed: true,
                resultingSlotWeapon: ref resultingWeapon);

            Assert.Equal(new[] { "pickup", "wield" }, ApplyCalls);
            Assert.Equal(new short[] { 3, 7 }, AppliedSlotAmounts);
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

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void IsWorldItemStateAvailable_RequiresAnActiveValidItem(
        bool isRemoved,
        bool isDeactivated,
        bool isGameEntityValid,
        bool expected)
    {
        Assert.Equal(
            expected,
            WeaponPickupHandler.IsWorldItemStateAvailable(
                isRemoved,
                isDeactivated,
                isGameEntityValid));
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

    private static void InitializeEquipmentSlot(
        Agent agent,
        EquipmentIndex equipmentIndex)
    {
        var equipment = new MissionEquipment();
        var weaponSlots = new MissionWeapon[(int)EquipmentIndex.NumAllWeaponSlots];
        var weapon = new MissionWeapon(
            ObjectHelper.SkipConstructor<ItemObject>(),
            null,
            null);
        object boxedWeapon = weapon;
        typeof(MissionWeapon)
            .GetField("_weapons", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(
                boxedWeapon,
                new List<WeaponComponentData>
                {
                    new WeaponComponentData(null, WeaponClass.Arrow, default),
                });
        weaponSlots[(int)equipmentIndex] = (MissionWeapon)boxedWeapon;
        typeof(MissionEquipment)
            .GetField(
                "_weaponSlots",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(equipment, weaponSlots);
        agent.InitializeMissionEquipment(equipment, null);
    }

    private static bool CapturePickup(
        SpawnedItemEntity worldItem,
        bool isSuccessful,
        int preferenceIndex)
    {
        ApplyCalls.Add("pickup");
        Assert.True(isSuccessful);
        pickedItemSlot = (EquipmentIndex)preferenceIndex;
        worldItemAmountAtPickup = worldItem.WeaponCopy.Amount;
        pickupRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }

    private static bool CaptureSlotAmount(short amount)
    {
        AppliedSlotAmounts.Add(amount);
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
