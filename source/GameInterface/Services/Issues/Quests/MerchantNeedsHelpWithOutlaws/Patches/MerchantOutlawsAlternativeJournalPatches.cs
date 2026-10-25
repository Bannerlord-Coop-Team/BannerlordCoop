using Common;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;
using GameInterface.Services.Issues.Framework.Visibility;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class MerchantOutlawsAlternativeJournalPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.OnAlternativeSolutionSolvedAndTroopsAreReturning))]
    [HarmonyPrefix]
    private static bool ReturningPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer || !(__instance is Issue)) return true;

        // The server sends this log, so a client tick must not append it again.
        __instance._areIssueEffectsResolved = true;
        return false;
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void DailyPrefix(IssueManager __instance, out List<IssueBase> __state)
    {
        __state = null;
        if (ModInformation.IsServer) return;
        // Owner progress comes from the server snapshot, regardless of which side ticks first.
        __state = __instance.Issues.Values.Where(issue => issue is Issue && issue.IsSolvingWithAlternative).ToList();
        QuestScreenIssueFilterPatch.SetState(__state, IssueBase.IssueState.SolvingWithQuestSolution);
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyFinalizer]
    private static void DailyFinalizer(List<IssueBase> __state)
    {
        if (__state != null) QuestScreenIssueFilterPatch.SetState(__state, IssueBase.IssueState.SolvingWithAlternativeSolution);
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPostfix]
    private static void DailyPostfix(IssueManager __instance)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) return;
        foreach (var issue in __instance.Issues.Values.OfType<Issue>()) progress.Send(issue);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void FinalizedPrefix(IssueBase __instance)
    {
        // Framework dismissal ends the issue directly, without finalizing its unstarted server quest.
        if (ModInformation.IsServer && __instance is Issue && __instance.IssueQuest is Quest quest && quest.IsOngoing)
        {
            quest.FinalizeQuest();
            quest.AfterFinalize();
        }

        if (__instance is Issue && ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(__instance);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPostfix]
    private static void FinalizedPostfix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue && ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress))
            progress.Forget(__instance);
    }

}
