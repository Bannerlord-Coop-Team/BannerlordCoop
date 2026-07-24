using Common;
using Common.Util;
using HarmonyLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

[Collection(nameof(Tournaments.TournamentCombatInfrastructureCollection))]
public class WeaponPickupSyncTests
{
    private static readonly List<string> ApplyCalls = new();
    private static EquipmentIndex equippedSlot;
    private static AgentEquipmentData appliedEquipment;
    private static bool equipRanInsideAllowedThread;
    private static bool runtimeEquipmentRanInsideAllowedThread;
    private static bool wieldRanInsideAllowedThread;

    [Fact]
    public void ReplicatedPickup_CarriesPostPickupWieldState()
    {
        var equipment = new AgentEquipmentData(
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon2,
            0);
        var message = new NetworkWeaponPickedup(
            System.Guid.NewGuid(),
            EquipmentIndex.Weapon2,
            null,
            null,
            null,
            equipment);
        PropertyInfo? property = typeof(NetworkWeaponPickedup).GetProperty(
            nameof(NetworkWeaponPickedup.CurrentEquipment));
        Assert.NotNull(property);
        ProtoMemberAttribute? member = property.GetCustomAttribute<ProtoMemberAttribute>();
        Assert.NotNull(member);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message.CurrentEquipment);
        stream.Position = 0;

        AgentEquipmentData received = Serializer.Deserialize<AgentEquipmentData>(stream);

        Assert.Equal(6, member.Tag);
        Assert.Equal((int)EquipmentIndex.Weapon0, received.MainHandIndex);
        Assert.Equal((int)EquipmentIndex.Weapon2, received.OffHandIndex);
        Assert.Equal(0, received.MainHandUsageIndex);
    }

    [Fact]
    public void ApplyWeaponPickup_EquipsShieldBeforeApplyingWieldState()
    {
        var harmony = new Harmony($"{nameof(ApplyWeaponPickup_EquipsShieldBeforeApplyingWieldState)}.{System.Guid.NewGuid()}");
        MethodInfo equip = AccessTools.Method(
            typeof(Agent),
            nameof(Agent.EquipWeaponWithNewEntity),
            new[] { typeof(EquipmentIndex), typeof(MissionWeapon).MakeByRefType() });
        MethodInfo wield = AccessTools.Method(
            typeof(AgentEquipmentData),
            nameof(AgentEquipmentData.Apply),
            new[] { typeof(Agent) });

        ApplyCalls.Clear();
        equippedSlot = EquipmentIndex.None;
        equipRanInsideAllowedThread = false;
        wieldRanInsideAllowedThread = false;

        try
        {
            harmony.Patch(
                equip,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(WeaponPickupSyncTests),
                    nameof(CaptureEquip))));
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
                EquipmentIndex.Weapon2,
                ref weapon,
                equipment);

            Assert.Equal(new[] { "equip", "wield" }, ApplyCalls);
            Assert.Equal(EquipmentIndex.Weapon2, equippedSlot);
            Assert.True(agentInfo.TryGetAuthoritativeEquipment(out AgentEquipmentData recorded));
            Assert.Equal(equipment, recorded);
            Assert.True(equipRanInsideAllowedThread);
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

    private static bool CaptureEquip(EquipmentIndex slotIndex)
    {
        ApplyCalls.Add("equip");
        equippedSlot = slotIndex;
        equipRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }

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
