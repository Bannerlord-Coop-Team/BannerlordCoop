using HarmonyLib;
using SandBox.ViewModelCollection;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(SPScoreboardVM), "OnTick")]
internal static class FinishedSimulationScoreboardTickPatch
{
    [HarmonyPrefix]
    private static bool StopFinishedSimulationTick(SPScoreboardVM __instance)
    {
        return ShouldContinueTick(__instance.IsSimulation, __instance.IsOver);
    }

    internal static bool ShouldContinueTick(bool isSimulation, bool isOver)
    {
        // The synchronized MapEvent can be destroyed while its finished scoreboard remains open.
        return !isSimulation || !isOver;
    }
}
