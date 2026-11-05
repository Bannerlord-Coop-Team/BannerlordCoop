using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

internal class HeadmanNeedsGrainAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy, IAlternativeSolutionAcceptPrecondition
{
    public bool CanAccept(IssueBase issue, Hero playerHero) =>
        playerHero.Gold >= ((Issue)issue).AlternativeSolutionNeededGold;

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }
}
