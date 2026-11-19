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
        var today = (int)CampaignTime.Now.ToDays;
        foreach (var issue in issueManager.Issues.Values)
        {
            if (!issue.IsSolvingWithAlternative || issue.AlternativeSolutionReturnTimeForTroops.IsPast)
                continue;

            var foreign = visibility.IsAnotherPlayersTroopsMission(issue);
            var clearDay = (int)issue.AlternativeSolutionIssueEffectClearTime.ToDays;
            // Only the owner gets return entries, and vanilla's progress update needs a first journal entry.
            if (foreign || issue.JournalEntries.Count == 0)
            {
                // A late joiner can miss the clear day, so resolve the effects on that day or later.
                if (clearDay <= today)
                    issue._areIssueEffectsResolved = true;

                continue;
            }

            if (clearDay == today)
                issue.OnAlternativeSolutionSolvedAndTroopsAreReturning();

            var journalLog = issue.JournalEntries[0];
            journalLog.UpdateCurrentProgress(Math.Min(journalLog.CurrentProgress + 1, journalLog.Range));
        }
    }
}
