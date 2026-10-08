using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(SmugglersIssueBehavior.SmugglersIssue), nameof(IssueBase.AlternativeSolutionCondition))]
internal class SmugglersAlternativeEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, ref TextObject explanation, ref bool __result)
    {
        if (ModInformation.IsClient || !AlternativeSolutionStartAuthorityGuard.IsActive
            || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (__instance.CheckPreconditions(__instance.IssueOwner, out explanation)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal class SmugglersAlternativeOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (ModInformation.IsServer && AlternativeSolutionStartAuthorityGuard.IsActive
            && __instance is SmugglersIssueBehavior.SmugglersIssue && __instance.IsSolvingWithAlternative
            && ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners))
            owners.Set(__instance, Hero.MainHero);
    }
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior), "OnIssueUpdated")]
internal class SmugglersAlternativeOutcomeJournalPatch
{
    [HarmonyPrefix]
    private static bool Prefix(JournalLogsCampaignBehavior __instance, IssueBase issue,
        IssueBase.IssueUpdateDetails details, Hero issueSolver)
    {
        if (ModInformation.IsClient || !IssueFinalizeAuthorityGuard.IsActive
            || issue is not SmugglersIssueBehavior.SmugglersIssue || details != IssueBase.IssueUpdateDetails.IssueCancel
            || issueSolver != null || !ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority)
            || !authority.IsRemovedOwner(issue)) return true;
        __instance.OnIssueLogAdded(issue, true);
        __instance.GetRelatedLog(issue).Update(__instance.GetEntries(issue), details);
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsClient || issue is not SmugglersIssueBehavior.SmugglersIssue
            || !issue.IsSolvingWithAlternative || details == IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest) return;
        MessageBroker.Instance.Publish(issue, new SmugglersAlternativeJournalChanged(issue, details));
    }
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class SmugglersAlternativeProgressJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient) return;
        foreach (var entry in __instance.Issues.ToArray())
        {
            if (entry.Value is not SmugglersIssueBehavior.SmugglersIssue issue || !issue.IsSolvingWithAlternative) continue;
            if (issue.AlternativeSolutionReturnTimeForTroops.IsPast)
                AlternativeSolutionCompletionRunner.CompleteOnServer(entry.Key, issue);
            else
                MessageBroker.Instance.Publish(issue, new SmugglersAlternativeJournalChanged(issue, IssueBase.IssueUpdateDetails.None));
        }
    }
}
