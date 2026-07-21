using GameInterface.Services.MapEvents;
using HarmonyLib;
using SandBox.ViewModelCollection;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Keeps the native dead-player scoreboard from fast-forwarding live coop battles.
/// </summary>
[HarmonyPatch(typeof(SPScoreboardVM), nameof(SPScoreboardVM.ExecuteFastForwardAction))]
internal static class DeadPlayerFastForwardPatch
{
    [HarmonyPrefix]
    private static bool PreventCoopBattleFastForward(SPScoreboardVM __instance)
    {
        if (!BattleSpawnGate.IsCoopBattleActive || __instance.IsSimulation)
            return true;

        __instance.IsFastForwarding = false;
        InformationManager.DisplayMessage(new InformationMessage(
            "Fast forwarding is disabled during cooperative battles."));
        return false;
    }
}
