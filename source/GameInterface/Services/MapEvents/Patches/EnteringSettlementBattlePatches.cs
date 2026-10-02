using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(WhileEnteringSettlementBattleMissionController))]
internal static class EnteringSettlementBattlePatches
{
    private static readonly ConditionalWeakTable<WhileEnteringSettlementBattleMissionController, object> deploymentReported = new();

    [HarmonyPatch(nameof(WhileEnteringSettlementBattleMissionController.OnMissionTick))]
    [HarmonyPrefix]
    private static bool TickPrefix(WhileEnteringSettlementBattleMissionController __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive || __instance._isMissionInitialized) return true;
        if (!ContainerProvider.TryResolve<IBattleHostRegistry>(out var hosts) ||
            !hosts.TryGet(BattleSpawnGate.ActiveMapEventId, out var assignment) || assignment.Epoch <= 0) return false;
        if (__instance._troopSuppliers[0] is not CoopTroopSupplier defender ||
            __instance._troopSuppliers[1] is not CoopTroopSupplier attacker) return false;
        if (!defender.IsPopulated || !attacker.IsPopulated || defender.AllocationRevision != attacker.AllocationRevision) return false;
        // The first tick consumes the complete reserve once, after the elected client receives the guards.
        return !hosts.IsHost(BattleSpawnGate.ActiveMapEventId) || defender.TotalTroops == defender.SideTotalTroops;
    }

    [HarmonyPatch(nameof(WhileEnteringSettlementBattleMissionController.OnMissionTick))]
    [HarmonyPostfix]
    private static void TickPostfix(WhileEnteringSettlementBattleMissionController __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive || !__instance._troopsInitialized ||
            deploymentReported.TryGetValue(__instance, out _)) return;
        deploymentReported.Add(__instance, new object());
        Mission.Current.OnDeploymentFinished();
    }

    [HarmonyPatch(nameof(WhileEnteringSettlementBattleMissionController.GetAllTroopsForSide))]
    [HarmonyPrefix]
    private static bool AllTroopsPrefix(WhileEnteringSettlementBattleMissionController __instance,
        BattleSideEnum side, ref IEnumerable<IAgentOriginBase> __result)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        __result = __instance._troopSuppliers[(int)side].GetAllTroops();
        return false;
    }

    [HarmonyPatch(nameof(WhileEnteringSettlementBattleMissionController.GetNumberOfPlayerControllableTroops))]
    [HarmonyPrefix]
    private static bool ControllableTroopsPrefix(WhileEnteringSettlementBattleMissionController __instance, ref int __result)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        var side = PartyBase.MainParty.Side;
        __result = __instance._troopSuppliers[(int)side].GetNumberOfPlayerControllableTroops();
        return false;
    }
}
