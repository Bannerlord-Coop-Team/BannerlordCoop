using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Feeds the frozen server budget into vanilla's per-mission spawn logic.</summary>
[HarmonyPatch]
internal static class ServerBattleSizePatches
{
    [HarmonyPatch(typeof(BannerlordConfig), nameof(BannerlordConfig.GetRealBattleSize))]
    [HarmonyPostfix]
    private static void GetRealBattleSizePostfix(ref int __result)
    {
        ApplyServerBattleSize(ref __result);
    }

    [HarmonyPatch(typeof(BannerlordConfig), nameof(BannerlordConfig.GetRealBattleSizeForSiege))]
    [HarmonyPostfix]
    private static void GetRealSiegeBattleSizePostfix(ref int __result)
    {
        ApplyServerBattleSize(ref __result);
    }

    private static void ApplyServerBattleSize(ref int battleSize)
    {
        if (!BattleSpawnGate.IsCoopBattleActive) return;

        battleSize = BattleSpawnGate.BattleSize;
    }
}
