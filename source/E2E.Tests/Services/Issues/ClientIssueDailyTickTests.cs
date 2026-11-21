using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class ClientIssueDailyTickTests : IDisposable
{
    private const int ReturnDays = 5;

    private readonly QuestScenario scenario;
    private E2ETestEnvironment TestEnvironment { get; }

    public ClientIssueDailyTickTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void TheClientDailyTickLeavesStayAliveChecksToTheServer()
    {
        var client = scenario.ClientA;
        using var preconditions = new StubIssuePreconditions(QuestScenario.IssueType);
        preconditions.FailStayAlive(client);
        using var stayAliveChecks = new MethodCallRecorder(Priority.First, true,
            AccessTools.Method(QuestScenario.IssueType, nameof(IssueBase.IssueStayAliveConditions)));

        client.Call(() =>
        {
            var notable = client.GetRegisteredObject<Hero>(scenario.NotableId);
            var issue = notable.Issue;
            Assert.True(issue.IsOngoingWithoutQuest);
            Assert.False(issue.IssueStayAliveConditions());
            stayAliveChecks.Clear();

            Campaign.Current.IssueManager.DailyTick();

            Assert.Equal(0, stayAliveChecks.CountFor(client));
            Assert.Same(issue, notable.Issue);
        });
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

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void TheDailyTickResolvesForeignTroopsMissionEffectsWithoutAddingALog(int clearDayOffset, bool effectsResolved)
    {
        scenario.ClientB.Call(() =>
        {
            // Start the harness clock on a day boundary, so "today" has not elapsed yet.
            Campaign.Current.MapTimeTracker._numTicks = 0;
            var issue = SendTroops(scenario.ClientB, owner: QuestScenario.ControllerA, withJournal: true, troopsStartReturningToday: true);
            using (new AllowedThread())
            {
                issue._areIssueEffectsResolved = false;
                issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.Days(clearDayOffset);
                issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Days(ReturnDays);
            }

            Assert.Equal(clearDayOffset < 0, issue.AlternativeSolutionIssueEffectClearTime.IsPast);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(issue.IsSolvingWithAlternative);
            Assert.Equal(effectsResolved, issue._areIssueEffectsResolved);
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

    [Fact]
    public void TheDailyTickAddsTheReturnEntryToThisPlayersTroopsMission()
    {
        scenario.ClientA.Call(() =>
        {
            var issue = SendTroops(scenario.ClientA, owner: QuestScenario.ControllerA, withJournal: true, troopsStartReturningToday: true);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(issue.IsSolvingWithAlternative);
            Assert.Single(issue.JournalEntries, entry => entry.LogText?.ToString() == "Your men should be on their way.");
            Assert.Equal(1, issue.JournalEntries[0].CurrentProgress);
        });
    }

    // The server completes every player's troops mission, so it ticks all of them
    [Fact]
    public void TheServersDailyTickStillAdvancesAPlayersTroopsMission()
    {
        scenario.Server.Call(() =>
        {
            var issue = SendTroops(scenario.Server, owner: QuestScenario.ControllerA, withJournal: true, troopsStartReturningToday: false);

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
