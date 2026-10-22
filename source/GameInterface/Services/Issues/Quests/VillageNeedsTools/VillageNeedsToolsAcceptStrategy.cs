using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

/// <summary>Uses the captured offer and the server's companion acceptance quote.</summary>
internal class VillageNeedsToolsAcceptStrategy : IQuestSolutionAcceptStrategy, IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }

    byte[] IAlternativeSolutionAcceptStrategy.Capture(IssueBase issue)
    {
        return BitConverter.GetBytes(((Issue)issue).RewardGold);
    }

    void IAlternativeSolutionAcceptStrategy.Apply(IssueBase issue, byte[] captured)
    {
        if (captured == null || captured.Length != sizeof(int) || issue.JournalEntries.Count == 0) return;

        // The initial log uses a local market price even though the server pays the companion reward.
        issue.JournalEntries[0].LogText.SetTextVariable("REWARD_GOLD", BitConverter.ToInt32(captured, 0));
    }
}
