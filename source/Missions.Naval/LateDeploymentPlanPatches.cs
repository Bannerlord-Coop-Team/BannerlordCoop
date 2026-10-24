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
    [HarmonyPrefix]
    private static bool Prefix() => !NavalLateDeploymentPlan.IsMaking;
}

/// <summary>[Game thread] Makes a battle side's deployment plan after deployment, without notifying mission listeners.</summary>
internal static class NavalLateDeploymentPlan
{
    internal static bool IsMaking { get; private set; }

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
