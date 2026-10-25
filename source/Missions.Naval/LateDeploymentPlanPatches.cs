using HarmonyLib;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// The host plans the AI fleet's side after deployment ended; the deployment views (boundary markers) that listen
// for plans are done by then and throw, and nothing else listens, so that one late plan is not announced.
[HarmonyPatch(typeof(Mission), nameof(Mission.OnDeploymentPlanMade))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class LateDeploymentPlanNotificationPatch
{
    // Skips the plan notification only while the late plan is being made.
    [HarmonyPrefix]
    private static bool Prefix() => !NavalLateDeploymentPlan.IsMaking;
}

/// <summary>[Game thread] Makes a battle side's deployment plan after deployment, without notifying mission listeners.</summary>
internal static class NavalLateDeploymentPlan
{
    // Whether the late plan is being made now, which the notification patch reads.
    internal static bool IsMaking { get; private set; }

    // Makes the side's deployment plan with the notification patch silencing it, also when the plan throws.
    internal static void MakeForSide(DefaultNavalMissionLogic navalLogic, BattleSideEnum side)
    {
        IsMaking = true;
        try
        {
            navalLogic.MakeDeploymentPlansForSide(side);
        }
        finally
        {
            IsMaking = false;
        }
    }
}
