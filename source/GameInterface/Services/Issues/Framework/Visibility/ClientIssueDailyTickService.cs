using Common;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Services.Issues.Framework.Interface;

namespace GameInterface.Services.Issues.Framework.Visibility;

internal sealed class ClientIssueDailyTickService : IClientIssueDailyTickService
{
    public void Apply(IssueManager issueManager)
    {
        foreach (var issue in issueManager.Issues.Values)
        {
            if (!issue.IsSolvingWithAlternative || issue.AlternativeSolutionReturnTimeForTroops.IsPast)
                continue;

            var foreign = QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(issue);
            if (foreign || issue.JournalEntries.Count == 0)
            {
                if (issue.AlternativeSolutionIssueEffectClearTime.IsPast)
                    issue._areIssueEffectsResolved = true;

                continue;
            }

            if ((int)issue.AlternativeSolutionIssueEffectClearTime.ToDays == (int)CampaignTime.Now.ToDays)
                issue.OnAlternativeSolutionSolvedAndTroopsAreReturning();

            var journalLog = issue.JournalEntries[0];
            journalLog.UpdateCurrentProgress(Math.Min(journalLog.CurrentProgress + 1, journalLog.Range));
        }
    }
}
