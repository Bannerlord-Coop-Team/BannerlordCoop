using Common.Util;
using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class ForeignTroopsMissionDailyTickTests : IDisposable
{
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public ForeignTroopsMissionDailyTickTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    // Another player's troops mission is only marked as taken on this client, it has no journal here
    [Fact]
    public void TheDailyTickLeavesAnotherPlayersTroopsMissionAlone()
    {
        scenario.ClientB.Call(() =>
        {
            Assert.True(scenario.ClientB.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            var issue = notable.Issue;

            using (new AllowedThread())
            {
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(5);
                issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.DaysFromNow(3);
            }

            Assert.Empty(issue.JournalEntries);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(issue.IsSolvingWithAlternative);
            Assert.Empty(issue.JournalEntries);
        });
    }
}
