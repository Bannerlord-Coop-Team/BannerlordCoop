using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

[HarmonyPatch]
internal class BountyHuntersJournalPatches
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
    [HarmonyPostfix]
    private static void QuestLogAdded(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest && !QuestSolutionStartAuthorityGuard.IsActive
            && __instance.QuestGiver.Issue is Issue issue)
            MessageBroker.Instance.Publish(issue, new BountyHuntersJournalChanged(issue, true));
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
    [HarmonyPostfix]
    private static void IssueLogAdded(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue && !AlternativeSolutionStartAuthorityGuard.IsActive)
            MessageBroker.Instance.Publish(__instance, new BountyHuntersJournalChanged(__instance, false));
    }

    [HarmonyPatch(typeof(JournalLog), nameof(JournalLog.UpdateCurrentProgress))]
    [HarmonyPostfix]
    private static void ProgressUpdated(JournalLog __instance)
    {
        if (ModInformation.IsClient) return;
        var issue = Campaign.Current.IssueManager.Issues.Values.OfType<Issue>()
            .FirstOrDefault(candidate => candidate.JournalEntries.Contains(__instance));
        if (issue != null) MessageBroker.Instance.Publish(issue, new BountyHuntersJournalChanged(issue, false));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
    [HarmonyPostfix]
    private static void IssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsServer && issue is Issue && !AlternativeSolutionStartAuthorityGuard.IsActive
            && !QuestSolutionStartAuthorityGuard.IsActive)
            MessageBroker.Instance.Publish(issue, new BountyHuntersJournalChanged(issue, false, details));
    }
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior))]
internal class BountyHuntersPersonalJournalEventPatches
{
    private static bool CanRecord(Hero giver)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (QuestSolutionStartAuthorityGuard.IsActive || AlternativeSolutionStartAuthorityGuard.IsActive
            || BountyHuntersOwnerScope.CurrentOwner != null) return true;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var owners)) return false;
        return ModInformation.IsServer
            ? owners.TryGetOwnerControllerId(giver, out _)
            : owners.IsLocalPeerOwner(giver);
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnIssueLogAdded))]
    [HarmonyPrefix]
    private static bool IssueLogAdded(IssueBase issue) => issue is not Issue || CanRecord(issue.IssueOwner);

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    private static bool IssueUpdated(IssueBase issue) => issue is not Issue || CanRecord(issue.IssueOwner);

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnQuestStarted))]
    [HarmonyPrefix]
    private static bool QuestStarted(QuestBase quest) => quest is not Quest || CanRecord(quest.QuestGiver);

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnQuestLogAdded))]
    [HarmonyPrefix]
    private static bool QuestLogAdded(QuestBase quest) => quest is not Quest || CanRecord(quest.QuestGiver);

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnQuestCompleted))]
    [HarmonyPrefix]
    private static bool QuestCompleted(QuestBase quest) =>
        quest is not Quest || IssueFinalizeAuthorityGuard.IsActive || CanRecord(quest.QuestGiver);
}
