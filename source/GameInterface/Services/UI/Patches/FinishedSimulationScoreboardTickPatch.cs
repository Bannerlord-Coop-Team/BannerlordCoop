using HarmonyLib;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(SPScoreboardVM), nameof(SPScoreboardVM.OnTick))]
internal static class FinishedSimulationScoreboardTickPatch
{
    [HarmonyPrefix]
    private static bool StopFinishedSimulationTick(SPScoreboardVM __instance)
    {
        // Finalization detaches the party before playback finishes updating the scoreboard.
        if (__instance.IsSimulation && MobileParty.MainParty?.MapEvent == null) return false;

        return ShouldContinueTick(__instance.IsSimulation, __instance.IsOver);
    }

    internal static bool ShouldContinueTick(bool isSimulation, bool isOver)
    {
        // The synchronized MapEvent can be destroyed while its finished scoreboard remains open.
        return !isSimulation || !isOver;
    }
}
