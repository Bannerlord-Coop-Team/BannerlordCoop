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
        // The synchronized MapEvent can be destroyed while its finished scoreboard remains open.
        return !__instance.IsSimulation || (MobileParty.MainParty?.MapEvent != null && !__instance.IsOver);
    }
}
