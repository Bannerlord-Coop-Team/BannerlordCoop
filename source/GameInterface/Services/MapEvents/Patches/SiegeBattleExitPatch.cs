using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// A siege defender leaving the mission surrenders the whole garrison in vanilla. With several players
/// in the fight one player's leave must stay a personal retreat, so the surrender result is downgraded
/// to the ordinary confirmation prompt; their troops are adopted through the normal authority migration.
/// </summary>
[HarmonyPatch(typeof(BattleEndLogic), nameof(BattleEndLogic.TryExit))]
internal class SiegeBattleExitPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref BattleEndLogic.ExitResult __result)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;

        if (__result == BattleEndLogic.ExitResult.SurrenderSiege)
        {
            __result = BattleEndLogic.ExitResult.NeedsPlayerConfirmation;
        }
    }
}
