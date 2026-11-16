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

    internal static bool ShouldContinueTick(bool isSimulation, bool isOver, bool eventStillLive)
    {
        if (!isSimulation) return true;
        if (isOver) return false;
        // The synced MapEvent can be destroyed before playback finishes; stop ticking once it is gone.
        return eventStillLive;
    }
}
