using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

/// <summary>Preserves the server's companion acceptance quote.</summary>
internal class VillageNeedsToolsAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy, IAlternativeSolutionAcceptPrecondition
{
    public bool CanAccept(IssueBase issue, Hero playerHero)
    {
        return playerHero.Gold >= ((Issue)issue).CostOfToolsForAlternativeSolution;
    }

    public byte[] Capture(IssueBase issue)
    {
        return BitConverter.GetBytes(((Issue)issue).RewardGold);
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (captured == null || captured.Length != sizeof(int) || issue.JournalEntries.Count == 0) return;

        // The initial log uses a local market price even though the server pays the companion reward.
        issue.JournalEntries[0].LogText.SetTextVariable("REWARD_GOLD", BitConverter.ToInt32(captured, 0));
    }
}
