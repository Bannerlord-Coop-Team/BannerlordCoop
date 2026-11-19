using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Services.Issues.Framework.Interface;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>Advances the client's troop missions while the server handles expiry and stay-alive checks.</summary>
internal sealed class ClientIssueDailyTickService : IClientIssueDailyTickService
{
    private readonly IIssueMissionVisibility visibility;

    public ClientIssueDailyTickService(IIssueMissionVisibility visibility)
    {
        this.visibility = visibility;
    }

    public void Apply(IssueManager issueManager)
    {
        foreach (var issue in issueManager.Issues.Values)
        {
            if (!issue.IsSolvingWithAlternative || issue.AlternativeSolutionReturnTimeForTroops.IsPast)
                continue;

            var foreign = visibility.IsAnotherPlayersTroopsMission(issue);
            // A joiner loads foreign journals from the save, but only their owner should get a return entry.
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
