using Common;
using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal static class LesserNobleRevoltConsequencePatches
{
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
        if (ContainerProvider.TryResolve<ILesserNobleRevoltWorldEventService>(out var service))
        {
            service.ForgetOwnerBattle(quest);
        }
        if (ModInformation.IsClient)
        {
            return;
        }

        var party = quest._lesserNobleParty;
        if (party?.IsActive == true)
        {
            if (AllowedThread.IsThisThreadAllowed())
            {
                // Accept rollback cancels under AllowedThread, so replicate destruction after that scope ends.
                GameThread.EnqueueSafe(() =>
                {
                    if (party.IsActive) DestroyPartyAction.Apply(null, party);
                }, context: nameof(LesserNobleRevoltConsequencePatches));
            }
            else
            {
                DestroyPartyAction.Apply(null, party);
            }
        }

        // A dismissed mirror quest never reaches AfterFinalize, so its trackers would stay in the save a joiner loads.
        Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(quest);
    }
}
