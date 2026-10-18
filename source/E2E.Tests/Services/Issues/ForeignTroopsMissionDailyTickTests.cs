using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class ForeignTroopsMissionDailyTickTests : IDisposable
{
    private const int ReturnDays = 5;

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
    public void TheDailyTickLeavesAnotherPlayersTroopsMissionWithoutAJournalAlone()
    {
        scenario.ClientB.Call(() =>
        {
            var issue = SendTroops(scenario.ClientB, owner: QuestScenario.ControllerA, withJournal: false, troopsStartReturningToday: false);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(issue.IsSolvingWithAlternative);
            Assert.Empty(issue.JournalEntries);
        });
    }

    // A client that joined later has the server's journal for it, the day the troops start returning adds nothing
    [Fact]
    public void TheDailyTickAddsNoLogToAnotherPlayersTroopsMission()
    {
        scenario.ClientB.Call(() =>
        {
            var issue = SendTroops(scenario.ClientB, owner: QuestScenario.ControllerA, withJournal: true, troopsStartReturningToday: true);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(issue.IsSolvingWithAlternative);
            var journal = Assert.Single(issue.JournalEntries);
            Assert.Equal(0, journal.CurrentProgress);
        });
    }

    [Fact]
    public void TheDailyTickStillAdvancesThisPlayersOwnTroopsMission()
    {
        scenario.ClientA.Call(() =>
        {
            var issue = SendTroops(scenario.ClientA, owner: QuestScenario.ControllerA, withJournal: true, troopsStartReturningToday: false);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(issue.IsSolvingWithAlternative);
            var journal = Assert.Single(issue.JournalEntries);
            Assert.Equal(1, journal.CurrentProgress);
        });
    }

    private IssueBase SendTroops(EnvironmentInstance client, string owner, bool withJournal, bool troopsStartReturningToday)
    {
        Assert.True(client.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
        var issue = notable.Issue;

        Assert.True(client.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, owner));

        using (new AllowedThread())
        {
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(ReturnDays);
            issue.AlternativeSolutionIssueEffectClearTime = troopsStartReturningToday ? CampaignTime.Now : CampaignTime.DaysFromNow(ReturnDays - 1);

            if (withJournal)
            {
                issue._journalEntries.Add(new JournalLog(CampaignTime.Now, new TextObject("Troops sent"), new TextObject("Return days"), 0, ReturnDays, LogType.Text));
            }
        }

        return issue;
    }
}
