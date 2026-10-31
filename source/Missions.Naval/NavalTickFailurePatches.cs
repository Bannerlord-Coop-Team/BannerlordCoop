using HarmonyLib;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// In a co-op naval battle the engine's async agent and team tick runs on a worker with no managed caller, so a throw there
/// crashed the client and left tickCompleted false, freezing the game thread in WaitTickCompletion (R46, R48 L6).
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalAgentTickFailurePatch
{
    // Static as the patch is; it keeps the failing mission only weakly.
    private static readonly INavalTickFailureLog Failures = new NavalTickFailureLog();

    // Logs the throw, finishes the tick and swallows it in a co-op naval battle; elsewhere vanilla's crash stands.
    [HarmonyFinalizer]
    internal static Exception Finalizer(Mission __instance, Exception __exception)
    {
        if (__exception == null || !CoopNavalMissionScope.IsActive) return __exception;

        __instance.tickCompleted = true;
        Failures.Report(__exception, "Mission agent and team tick (finished the tick)", __instance);
        return null;
    }
}
