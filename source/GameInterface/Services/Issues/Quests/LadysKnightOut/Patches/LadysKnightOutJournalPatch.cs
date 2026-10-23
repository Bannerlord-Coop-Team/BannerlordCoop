using Common;
using Common.Messaging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch]
internal class LadysKnightOutJournalPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueLogAdded))]
    [HarmonyPostfix]
    private static void IssueLogPostfix(IssueBase issue)
    {
        if (ModInformation.IsServer && issue is Issue && issue.IsSolvingWithLordSolution)
            MessageBroker.Instance.Publish(issue, new LadysKnightOutIssueLogAdded(issue));
    }
}
