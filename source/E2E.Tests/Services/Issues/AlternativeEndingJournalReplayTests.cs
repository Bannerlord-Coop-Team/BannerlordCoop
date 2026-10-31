using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using ProtoBuf;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
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

    [Fact]
    public void AlternativeAcceptance_ReconcilesTheNativeHistoryWithoutRepeatingIssueUpdates()
    {
        var updates = new List<IssueBase.IssueUpdateDetails>();
        scenario.ClientA.Call(() =>
        {
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this,
                (IssueBase issue, IssueBase.IssueUpdateDetails detail, Hero solver) => updates.Add(detail));
            issues[scenario.ClientA]._journalEntries.Add(new JournalLog(new CampaignTime(999), new TextObject("Local acceptance")));
            archives[scenario.ClientA].Update(issues[scenario.ClientA].JournalEntries,
                IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest);
        });
        var text = new TextObject("{=acceptance_test}Troops for {PERSON}: {COUNT}", new Dictionary<string, object>
        {
            ["PERSON"] = new TextObject("Companion {NAME}", new Dictionary<string, object> { ["NAME"] = "Suvys" }),
            ["COUNT"] = 3,
            ["FRACTION"] = 1.5f,
            ["OPTIONAL"] = null!,
        });
        var task = new TextObject("Return {DAYS}", new Dictionary<string, object> { ["DAYS"] = 4 });
        var expected = new[]
        {
            new JournalLog(new CampaignTime(123), text, task, 2, 4),
            new JournalLog(new CampaignTime(124), null!, null!, 3, 5),
        };
        var message = new NetworkAlternativeSolutionJournal(scenario.NotableId, QuestScenario.IssueId,
            IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest,
            expected.Select(log => new AlternativeSolutionJournalEntry(log)).ToArray());
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPeer(QuestScenario.ControllerA, out var peer));
            scenario.Server.Resolve<INetwork>().Send(peer, message);
        });
        environment.FlushCoalescer();
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        scenario.ClientA.SimulateMessage(scenario.Server, message);
        scenario.ClientB.SimulateMessage(scenario.Server, message);
        Assert.Empty(updates);
        scenario.ClientB.Call(() =>
        {
            Assert.Same(issues[scenario.ClientB], issues[scenario.ClientB].IssueOwner.Issue);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(issues[scenario.ClientB])));
        });
        scenario.ClientA.Call(() =>
        {
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(issues[scenario.ClientA])));
            Assert.Same(archives[scenario.ClientA], archive);
            Assert.False(archive.IsEnded());
            Assert.Equal(IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, archive._lastIssueStatus);
            foreach (var entries in new[] { archive.GetEntries().ToArray(), issues[scenario.ClientA].JournalEntries.ToArray() })
            {
                Assert.Equal(expected.Length, entries.Length);
                for (var index = 0; index < expected.Length; index++)
                {
                    Assert.Equal(expected[index].LogTime, entries[index].LogTime);
                    Assert.Equal(expected[index].CurrentProgress, entries[index].CurrentProgress);
                    Assert.Equal(expected[index].Range, entries[index].Range);
                    Assert.Equal(expected[index].Type, entries[index].Type);
                    Assert.Equal(expected[index].LogText?.Value, entries[index].LogText?.Value);
                    Assert.Equal(expected[index].TaskName?.Value, entries[index].TaskName?.Value);
                }
                Assert.Equal(text.ToString(), entries[0].LogText.ToString());
                Assert.Null(entries[1].LogText);
                Assert.Null(entries[1].TaskName);
                var person = Assert.IsType<TextObject>(entries[0].LogText.Attributes["PERSON"]);
                Assert.Equal("Suvys", person.Attributes["NAME"]);
                Assert.Equal(3, entries[0].LogText.Attributes["COUNT"]);
                Assert.Equal(1.5f, entries[0].LogText.Attributes["FRACTION"]);
                Assert.Null(entries[0].LogText.Attributes["OPTIONAL"]);
                Assert.Equal(4, entries[0].TaskName.Attributes["DAYS"]);
            }
        });
    }

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
    public void AlternativeCancellationArchivesTroopReturnOnceForTheOwnerOnly()
    {
        foreach (var instance in environment.Clients.Prepend(environment.Server))
            instance.Call(() => issues[instance].IsTriedToSolveBefore = true);

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            using (new AllowedThread()) owner.PartyBelongedTo = party;
            issues[scenario.Server].CompleteIssueWithCancel();
        });
        environment.FlushCoalescer();

        var journal = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, journal.Ending);
        Assert.Single(journal.Entries, entry => entry.Text == "Your troops have returned from their mission.");

        foreach (var instance in new[] { scenario.Server, scenario.ClientA })
        {
            instance.Call(() =>
            {
                var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry.IsRelatedTo(issues[instance])));
                Assert.True(archive.IsEnded());
                Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, archive._lastIssueStatus);
                Assert.Single(archive.GetEntries(), entry =>
                    entry.LogText?.ToString() == "Your troops have returned from their mission.");
            });
        }

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

    [Theory]
    [InlineData(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest)]
    [InlineData(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest)]
    public void AlternativeCompletion_PreservesNativeTextTemplatesAndAttributes(IssueBase.IssueUpdateDetails ending)
    {
        var logTime = new CampaignTime(456L);
        var person = new TextObject("Companion {NAME}", new Dictionary<string, object> { ["NAME"] = "Suvys" });
        var text = new TextObject("Mission for {PERSON}: {COUNT}", new Dictionary<string, object>
        {
            ["PERSON"] = person,
            ["COUNT"] = 3,
        });
        var task = new TextObject("Return {DAYS}", new Dictionary<string, object> { ["DAYS"] = 4 });
        scenario.Server.Call(() => issues[scenario.Server].AddLog(new JournalLog(logTime, text, task, 3, 4)));

        EndOnServer(ending);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        AssertObserverHasNoArchive();
        scenario.ClientA.Call(() =>
        {
            var archive = archives[scenario.ClientA];
            Assert.True(archive.IsEnded());
            var entries = archive.GetEntries().ToArray();
            Assert.Equal(2, entries.Length);
            var progress = entries[0];
            Assert.Equal(logTime.NumTicks, progress.LogTime.NumTicks);
            Assert.Equal(3, progress.CurrentProgress);
            Assert.Equal(4, progress.Range);
            Assert.Equal(text.ToString(), progress.LogText.ToString());
            Assert.Equal(task.ToString(), progress.TaskName.ToString());
            Assert.Equal(text.Value, progress.LogText.Value);
            Assert.Equal(2, progress.LogText.Attributes.Count);
            Assert.Equal(3, Assert.IsType<int>(progress.LogText.Attributes["COUNT"]));
            var actualPerson = Assert.IsType<TextObject>(progress.LogText.Attributes["PERSON"]);
            Assert.Equal(person.Value, actualPerson.Value);
            Assert.Equal("Suvys", Assert.IsType<string>(actualPerson.Attributes["NAME"]));
            Assert.Equal(task.Value, progress.TaskName.Value);
            Assert.Equal(4, Assert.IsType<int>(progress.TaskName.Attributes["DAYS"]));
            Assert.Equal(EndingText(ending).ToString(), entries[1].LogText.ToString());
        });
    }

    [Fact]
    public void AlternativeCompletion_RefusesStaleIdentityAndNonEndingStatus()
    {
        foreach (var message in new[]
        {
            new NetworkAlternativeSolutionJournal(scenario.NotableId, "old_issue", IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, null!),
            new NetworkAlternativeSolutionJournal(scenario.NotableId, QuestScenario.IssueId, IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution, null!),
        })
            scenario.ClientA.SimulateMessage(scenario.Server, message);

        scenario.ClientA.Call(() =>
        {
            Assert.Same(issues[scenario.ClientA], issues[scenario.ClientA].IssueOwner.Issue);
            Assert.False(archives[scenario.ClientA].IsEnded());
        });
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Previous text", "Previous task")]
    public void AlternativeJournal_ReplaysPreviousPayloadWithoutNativeText(string text, string task)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new PreviousJournalEntry { Text = text, TaskName = task });
        stream.Position = 0;
        var received = Serializer.Deserialize<AlternativeSolutionJournalEntry>(stream);
        var log = received.ToJournalLog();

        Assert.Null(received.NativeText);
        Assert.Null(received.NativeTaskName);
        Assert.Equal(text, log.LogText?.Value);
        Assert.Equal(task, log.TaskName?.Value);
    }

    [Fact]
    public void AlternativeJournal_PreviousReadersKeepAllSixRenderedFields()
    {
        var text = new TextObject("{=acceptance_compat}Troops for {NAME}");
        text.SetTextVariable("NAME", "Suvys");
        var task = new TextObject("{=acceptance_task}Return {DAYS}");
        task.SetTextVariable("DAYS", 4);
        var native = new JournalLog(new CampaignTime(123L), text, task, 2, 4, LogType.Text);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new AlternativeSolutionJournalEntry(native));
        stream.Position = 0;
        var previous = Serializer.Deserialize<PreviousJournalEntry>(stream);
        Assert.Equal(123L, previous.Time);
        Assert.Equal(text.ToString(), previous.Text);
        Assert.Equal(task.ToString(), previous.TaskName);
        Assert.Equal(2, previous.Progress);
        Assert.Equal(4, previous.Range);
        Assert.Equal(native.Type, previous.Type);
    }

    [ProtoContract]
    private class PreviousJournalEntry
    {
        [ProtoMember(1)] public long Time { get; set; }
        [ProtoMember(2)] public string Text { get; set; }
        [ProtoMember(3)] public string TaskName { get; set; }
        [ProtoMember(4)] public int Progress { get; set; }
        [ProtoMember(5)] public int Range { get; set; }
        [ProtoMember(6)] public LogType Type { get; set; }
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
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
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
