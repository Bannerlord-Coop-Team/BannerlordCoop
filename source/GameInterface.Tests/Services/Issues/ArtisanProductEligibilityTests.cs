using Common.Tests.Utils;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

public sealed class ArtisanProductEligibilityTests
{
    [Theory]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution, "player-a", true)]
    [InlineData(IssueBase.IssueState.SolvingWithAlternativeSolution, "player-a", true)]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution, "player-b", false)]
    [InlineData(IssueBase.IssueState.SolvingWithAlternativeSolution, "player-b", false)]
    [InlineData(IssueBase.IssueState.Ongoing, "player-a", false)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution, "player-a", false)]
    public void DuplicateEligibilityCountsOnlyTheSamePlayersQuestOrCompanionSolution(
        IssueBase.IssueState state, string owner, bool expected)
    {
        var registry = new IssueOwnershipRegistry();
        var candidate = ObjectHelper.SkipConstructor<Issue>();
        candidate._issueOwner = ObjectHelper.SkipConstructor<Hero>();
        candidate._issueState = state;
        registry.SetOwner(candidate.IssueOwner, owner);
        var acceptance = new ArtisanProductQuestAcceptance(null, null, null, registry);

        Assert.Equal(expected, acceptance.HasConflictingIssue(new[] { candidate },
            ObjectHelper.SkipConstructor<Issue>(), "player-a"));
        Assert.False(acceptance.HasConflictingIssue(new[] { candidate }, candidate, "player-a"));
    }
}
