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
        return ShouldContinueTick(__instance.IsSimulation, __instance.IsOver, MobileParty.MainParty?.MapEvent != null);
    }

    internal static bool ShouldContinueTick(bool isSimulation, bool isOver, bool hasMapEvent)
    {
        // The synchronized MapEvent can be destroyed while its finished scoreboard remains open.
        return !isSimulation || (hasMapEvent && !isOver);
    }
}
