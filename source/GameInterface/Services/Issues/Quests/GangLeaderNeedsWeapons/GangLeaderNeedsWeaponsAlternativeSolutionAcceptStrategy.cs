using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

// The alternative solution has fixed math (no randomness)
internal class GangLeaderNeedsWeaponsAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy, IAlternativeSolutionAcceptPrecondition
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }

    public bool CanAccept(IssueBase issue, Hero playerHero)
    {
        return playerHero.Gold >= ((Issue)issue).CompanionGoldNeedForAlternativeSolution;
    }
}
