using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments.Patches;

internal static class TournamentCombatPatchInstaller
{
    private const string HarmonyId = "Coop.TournamentCombat";

    public static void Install()
    {
        var target = AccessTools.Method(typeof(Agent), nameof(Agent.RegisterBlow));
        if (target == null) return;
        if (Harmony.GetPatchInfo(target)?.Prefixes.Any(patch => patch.owner == HarmonyId) == true) return;

        var prefix = AccessTools.Method(typeof(TournamentBlowInterceptPatch), nameof(TournamentBlowInterceptPatch.Prefix));
        var harmony = new Harmony(HarmonyId);
        harmony.Patch(target, prefix: new HarmonyMethod(prefix));

        var reward = AccessTools.Method(typeof(TournamentFightMissionController), "EnemyHitReward");
        var rewardPrefix = AccessTools.Method(typeof(TournamentRewardSuppressionPatch), nameof(TournamentRewardSuppressionPatch.Prefix));
        if (reward != null)
            harmony.Patch(reward, prefix: new HarmonyMethod(rewardPrefix));

        var leave = AccessTools.Method(typeof(TournamentBehavior), "EndTournamentViaLeave");
        var leavePrefix = AccessTools.Method(typeof(TournamentLeaveGuardPatch), nameof(TournamentLeaveGuardPatch.Prefix));
        if (leave != null)
            harmony.Patch(leave, prefix: new HarmonyMethod(leavePrefix));

        var setKilled = AccessTools.Method(
            typeof(TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin),
            nameof(TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin.SetKilled));
        var campaignMutationPrefix = AccessTools.Method(
            typeof(TournamentCampaignOriginGuardPatch),
            nameof(TournamentCampaignOriginGuardPatch.Prefix));
        if (setKilled != null)
            harmony.Patch(setKilled, prefix: new HarmonyMethod(campaignMutationPrefix));
    }
}

internal static class TournamentCampaignOriginGuardPatch
{
    public static bool Prefix()
        => Mission.Current?.GetMissionBehavior<CoopTournamentController>() == null;
}

internal static class TournamentLeaveGuardPatch
{
    public static bool Prefix(TournamentBehavior __instance)
    {
        if (__instance is not CoopTournamentBehavior) return true;
        return false;
    }
}

internal static class TournamentRewardSuppressionPatch
{
    public static bool Prefix()
        => Mission.Current?.GetMissionBehavior<CoopTournamentController>() == null;
}

internal static class TournamentBlowInterceptPatch
{
    public static bool Prefix(Agent __instance, Blow blow, ref AttackCollisionData collisionData)
    {
        CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
        if (controller == null) return true;
        return controller.InterceptBlow(__instance, blow, collisionData);
    }
}
