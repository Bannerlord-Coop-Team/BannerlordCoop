using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltConsequencePatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionStartConsequence))]
    [HarmonyPrefix]
    private static bool AlternativeStart(IssueBase __instance) => __instance is not Issue || ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizeParty(Quest __instance)
    {
        ReleaseQuest(__instance);
        return false;
    }

    // Dismissing a quest whose player left calls IssueFinalized, which drops IssueQuest without
    // finalizing the quest, so the revolt party and its battle record are released here instead.
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void ReleaseDismissedQuest(IssueBase __instance)
    {
        if (__instance is Issue && __instance.IssueQuest is Quest quest)
        {
            ReleaseQuest(quest);
        }
    }

    internal static void ReleaseQuest(Quest quest)
    {
        LesserNobleRevoltWorldEventPatches.ForgetOwnerBattle(quest);
        if (ModInformation.IsClient)
        {
            return;
        }

        if (quest._lesserNobleParty?.IsActive == true)
        {
            DestroyPartyAction.Apply(null, quest._lesserNobleParty);
        }
    }
}
