using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Applies the frozen co-op human budget to vanilla reinforcement pulls.</summary>
[HarmonyPatch(typeof(MissionBattleSideSpawnContext))]
internal static class CoopBattleReinforcementCapPatch
{
    [HarmonyPatch(nameof(MissionBattleSideSpawnContext.CheckReinforcementBatch))]
    [HarmonyPrefix]
    private static bool CheckReinforcementBatchPrefix(ref bool __result)
    {
        if (!ShouldPauseReinforcements()) return true;

        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(MissionBattleSideSpawnContext.TryReinforcementSpawn))]
    [HarmonyPrefix]
    private static bool TryReinforcementSpawnPrefix(ref int __result)
    {
        if (!ShouldPauseReinforcements()) return true;

        __result = 0;
        return false;
    }

    private static bool ShouldPauseReinforcements()
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return false;

        return BattleSpawnGate.HasPendingPrioritySpawn
            || !BattleSpawnGate.HasAvailableHumanAgentSlot(Mission.Current);
    }
}
