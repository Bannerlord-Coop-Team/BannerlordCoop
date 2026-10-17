using Common;
using Common.Messaging;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch]
internal class LadysKnightOutJournalPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnQuestLogAdded))]
    [HarmonyPostfix]
    private static void Postfix(QuestBase quest)
    {
        if (ModInformation.IsServer && quest is Quest && quest.JournalEntries.Count > 0)
            MessageBroker.Instance.Publish(quest, new LadysKnightOutLogAdded(quest, quest.JournalEntries[quest.JournalEntries.Count - 1]));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueLogAdded))]
    [HarmonyPostfix]
    private static void IssueLogPostfix(IssueBase issue)
    {
        if (ModInformation.IsServer && issue is Issue && issue.IsSolvingWithLordSolution)
            MessageBroker.Instance.Publish(issue, new LadysKnightOutIssueLogAdded(issue));
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    private static bool IssueRewardsPrefix(IssueBase issue) => !(issue is Issue) || ModInformation.IsServer;

    [HarmonyPatch(typeof(JournalLogsCampaignBehavior), nameof(JournalLogsCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    private static bool HistoryPrefix(IssueBase issue) => !(issue is Issue) || issue.IsTriedToSolveBefore;

    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    private static bool IssueNotificationPrefix(IssueBase issue) => !(issue is Issue) ||
        (ModInformation.IsClient && issue.IsTriedToSolveBefore);

    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnQuestCompleted))]
    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnQuestLogAdded))]
    [HarmonyPrefix]
    private static bool QuestNotificationPrefix(QuestBase quest) => !(quest is Quest) || ModInformation.IsClient;
}
