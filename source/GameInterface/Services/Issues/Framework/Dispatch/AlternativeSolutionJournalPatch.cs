using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
internal class AlternativeSolutionJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsServer &&
            (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest))
            MessageBroker.Instance.Publish(issue, new AlternativeSolutionEndingObserved(issue, details));
    }
}
