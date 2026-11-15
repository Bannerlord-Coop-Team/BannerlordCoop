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
        // The synchronized MapEvent can be destroyed while the scoreboard is still open, finished or not.
        bool eventStillLive = MobileParty.MainParty?.MapEvent != null;
        return !__instance.IsSimulation || (eventStillLive && !__instance.IsOver);
    }
}
