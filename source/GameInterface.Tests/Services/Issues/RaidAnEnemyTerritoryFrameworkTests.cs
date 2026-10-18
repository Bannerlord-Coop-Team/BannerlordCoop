using GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Tests.Services.Issues;

public class RaidAnEnemyTerritoryFrameworkTests
{
    [Fact]
    public void AcceptCapture_PreservesTheQuestDeadline()
    {
        var source = UninitializedQuest();
        source.ChangeQuestDueTime(new CampaignTime(1234));

        var strategy = new RaidAnEnemyTerritoryQuestSolutionAcceptStrategy();
        var target = UninitializedQuest();
        strategy.Apply(IssueWith(target), strategy.Capture(IssueWith(source)));

        Assert.Equal(source.QuestDueTime.NumTicks, target.QuestDueTime.NumTicks);
    }

    [Fact]
    public void FinalizationProof_RejectsAnUnknownBranch()
    {
        var strategy = new RaidAnEnemyTerritoryFinalizationProofStrategy();
        Assert.False(strategy.TryRunBranch(UninitializedQuest(), 0));
    }

    private static IssueBase IssueWith(Quest quest)
    {
        var issue = (IssueBase)RuntimeHelpers.GetUninitializedObject(typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue));
        issue.IssueQuest = quest;
        return issue;
    }

    private static Quest UninitializedQuest() =>
        (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
}
