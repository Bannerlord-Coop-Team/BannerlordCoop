using GameInterface;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// The host plans the AI fleet's side after deployment ended; the deployment views (boundary markers) that listen
// for plans are done by then and throw, and nothing else listens, so that one late plan is not announced.
[HarmonyPatch(typeof(Mission), nameof(Mission.OnDeploymentPlanMade))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class LateDeploymentPlanNotificationPatch
{
    // Skips the plan notification only while this client's late plan is being made.
    [HarmonyPrefix]
    private static bool Prefix() =>
        !ContainerProvider.TryResolve<INavalLateDeploymentPlanTracker>(out var latePlans) || !latePlans.IsMaking;
}
