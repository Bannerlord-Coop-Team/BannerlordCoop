using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>Clients advance their own issues while the server owns expiry and foreign missions.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class ClientIssueDailyTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueManager __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        foreach (var issue in __instance.Issues.Values)
        {
            if (!issue.IsSolvingWithAlternative || issue.AlternativeSolutionReturnTimeForTroops.IsPast)
            {
                continue;
            }

            var foreign = QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(issue);
            if (foreign || issue.JournalEntries.Count == 0)
            {
                if (issue.AlternativeSolutionIssueEffectClearTime.IsPast)
                {
                    issue._areIssueEffectsResolved = true;
                }

                continue;
            }

            if ((int)issue.AlternativeSolutionIssueEffectClearTime.ToDays == (int)CampaignTime.Now.ToDays)
            {
                issue.OnAlternativeSolutionSolvedAndTroopsAreReturning();
            }

            var journalLog = issue.JournalEntries[0];
            journalLog.UpdateCurrentProgress(Math.Min(journalLog.CurrentProgress + 1, journalLog.Range));
        }

        return false;
    }
}
