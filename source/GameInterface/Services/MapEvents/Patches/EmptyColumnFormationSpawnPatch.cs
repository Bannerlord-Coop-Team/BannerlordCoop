using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Lets the first reinforcement into an emptied column use vanilla's formation spawn frame.</summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.GetUnitSpawnFrameWithIndex))]
internal class EmptyColumnFormationSpawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Formation __instance,
        ref WorldPosition? unitSpawnPosition,
        ref Vec2? unitSpawnDirection)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive)
            return true;
        if (!(__instance.Arrangement is ColumnFormation columnFormation))
            return true;
        if (columnFormation.GetUnitPositionsOnVanguardFileIndex().Count > 0)
            return true;

        unitSpawnPosition = null;
        unitSpawnDirection = null;
        return false;
    }
}
