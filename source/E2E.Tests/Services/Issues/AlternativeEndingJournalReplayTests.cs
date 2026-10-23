using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Reproduces alternative-solution archive loss through the framework outcome replay.</summary>
public class AlternativeEndingJournalReplayTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly Dictionary<EnvironmentInstance, IssueBase> issues = new();
    private readonly Dictionary<EnvironmentInstance, JournalLogEntry> archives = new();

    public AlternativeEndingJournalReplayTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            instance.Call(() =>
            {
                var giver = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                var issue = issues[instance] = giver.Issue;
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                    scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));

                new JournalLogsCampaignBehavior().RegisterEvents();
                if (instance == scenario.ClientB) return;

                // Only archive setup is synthetic; completion uses the normal callback and network path.
                var archive = archives[instance] = new JournalLogEntry(
                    new TextObject("Alternative mission"), giver, null, false, issue);
                archive.Update(new MBList<JournalLog>(), IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest);
                Campaign.Current.LogEntryHistory._logs.Add(archive);
                Assert.False(archive.IsEnded());
            });
        }
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest)]
    [InlineData(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest)]
    public void AlternativeCompletion_ArchivesTheOwnersEnding(IssueBase.IssueUpdateDetails ending)
    {
        EndOnServer(ending);

        // The observer must remain isolated even when the owner's archive assertion fails.
        AssertObserverHasNoArchive();
        scenario.ClientA.Call(() =>
        {
            Assert.Null(issues[scenario.ClientA].IssueOwner.Issue);
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog(
                (JournalLogEntry entry) => entry.IsRelatedTo(issues[scenario.ClientA]));
            Assert.Same(archives[scenario.ClientA], archive);
            Assert.True(archive.IsEnded());
            Assert.Equal(ending, archive._lastIssueStatus);
            Assert.Equal(ending == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, archive.IsEndedUnsuccessfully());
            Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == EndingText(ending).ToString());
        });
    }

    [Fact]
    public void AlternativeCompletion_DoesNotCreateAnObserversArchive()
    {
        EndOnServer(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest);
        AssertObserverHasNoArchive();
    }

    [Fact]
    public void AlternativeCompletion_PreservesProgressAndIgnoresLateDuplicateDelivery()
    {
        var logTime = new CampaignTime(123L);
        scenario.Server.Call(() => issues[scenario.Server].AddLog(new JournalLog(logTime,
            new TextObject("Troops are returning."), new TextObject("Return days"), 3, 4)));
        EndOnServer(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest);
        var message = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        scenario.ClientA.SimulateMessage(scenario.Server, message);
        scenario.ClientB.SimulateMessage(scenario.Server, message);
        scenario.ClientA.Call(() =>
        {
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(issues[scenario.ClientA])));
            Assert.True(archive.IsEnded());
            Assert.Equal(2, archive.GetEntries().Count());
            var progress = archive.GetEntries().First();
            Assert.Equal(logTime.NumTicks, progress.LogTime.NumTicks);
            Assert.Equal("Return days", progress.TaskName.ToString());
            Assert.Equal(3, progress.CurrentProgress);
            Assert.Equal(4, progress.Range);
        });
        AssertObserverHasNoArchive();
    }

    [Fact]
    public void AlternativeCompletion_PreservesTextlessEntries()
    {
        scenario.Server.Call(() => issues[scenario.Server].AddLog(new JournalLog(
            CampaignTime.Now, null!, null!, 3, 4)));
        EndOnServer(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        scenario.ClientA.Call(() =>
        {
            var archive = archives[scenario.ClientA];
            Assert.True(archive.IsEnded());
            var entries = archive.GetEntries().ToArray();
            Assert.Equal(2, entries.Length);
            Assert.Null(entries[0].LogText);
            Assert.Null(entries[0].TaskName);
            Assert.Equal(3, entries[0].CurrentProgress);
            Assert.Equal(4, entries[0].Range);
            Assert.Equal(EndingText(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest).ToString(), entries[1].LogText.ToString());
        });
        AssertObserverHasNoArchive();
    }

    [Fact]
    public void AlternativeCompletion_RefusesStaleIdentityAndNonEndingStatus()
    {
        foreach (var message in new[]
        {
            new NetworkAlternativeSolutionJournal(scenario.NotableId, "old_issue", IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, null!),
            new NetworkAlternativeSolutionJournal(scenario.NotableId, QuestScenario.IssueId, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, null!),
        })
            scenario.ClientA.SimulateMessage(scenario.Server, message);

        scenario.ClientA.Call(() =>
        {
            Assert.Same(issues[scenario.ClientA], issues[scenario.ClientA].IssueOwner.Issue);
            Assert.False(archives[scenario.ClientA].IsEnded());
        });
    }

    private void EndOnServer(IssueBase.IssueUpdateDetails ending)
    {
        scenario.Server.Call(() =>
        {
            var issue = issues[scenario.Server];
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            var hero = scenario.Server.GetRegisteredObject<Hero>(player.HeroId);
            using (var owner = new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                issue.AddLog(new JournalLog(CampaignTime.Now, EndingText(ending)));
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, ending, hero);
                Assert.True(archives[scenario.Server].IsEnded());
                Assert.Equal(ending, archives[scenario.Server]._lastIssueStatus);
                issue.IssueFinalized();
                scenario.Server.Resolve<IOutcomeDispatcher>().AfterAlternativeSolutionCompletion(issue, owner);
            }
        });
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.AlternativeSolution,
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    private void AssertObserverHasNoArchive()
    {
        scenario.ClientB.Call(() =>
        {
            Assert.Null(issues[scenario.ClientB].IssueOwner.Issue);
            Assert.Null(Campaign.Current.LogEntryHistory.FindLastGameActionLog(
                (JournalLogEntry entry) => entry.IsRelatedTo(issues[scenario.ClientB])));
        });
    }

    private static TextObject EndingText(IssueBase.IssueUpdateDetails ending) => new TextObject(
        ending == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest
            ? "The companion completed the mission."
            : "The companion failed the mission.");
}
