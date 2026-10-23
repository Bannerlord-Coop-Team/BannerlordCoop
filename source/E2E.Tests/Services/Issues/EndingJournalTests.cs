using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Exercises journal capture and archival through the serialized finalization messages.</summary>
public class EndingJournalTests : IDisposable
{
    private readonly StubIssueQuestGenerator quests = new();
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly Dictionary<EnvironmentInstance, List<QuestBase>> completions = new();

    public EndingJournalTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            var completed = completions[instance] = new List<QuestBase>();
            instance.Call(() =>
            {
                new JournalLogsCampaignBehavior().RegisterEvents();
                CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(completed, (quest, _) => completed.Add(quest));
            });
        }
        scenario.Accept(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread()) scenario.QuestOf(scenario.ClientA).StartQuest();
        });
    }

    public void Dispose()
    {
        environment.Dispose();
        preconditions.Dispose();
        quests.Dispose();
    }

    [Theory]
    [InlineData(IssueOutcome.QuestSuccess)]
    [InlineData(IssueOutcome.QuestFail)]
    [InlineData(IssueOutcome.QuestBetrayal)]
    [InlineData(IssueOutcome.QuestCancel)]
    [InlineData(IssueOutcome.QuestTimeOut)]
    public void ServerEnding_IsArchivedOnceOnlyForTheOwner(IssueOutcome outcome)
    {
        var serverQuest = scenario.QuestOf(scenario.Server);
        var ownerQuest = scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() => ownerQuest.AddLog(new TextObject("owner-only progress")));
        scenario.Server.Call(() =>
        {
            serverQuest._journalEntries.Add(new JournalLog(CampaignTime.Now, null!));
            serverQuest.AddLog(Ending());
            Complete(serverQuest, outcome);
        });
        environment.FlushCoalescer();

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        var snapshot = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        var sent = scenario.Server.NetworkSentMessages.ToList();
        Assert.True(sent.FindIndex(message => message is NetworkIssueJournal) < sent.FindIndex(message => message is NetworkIssueFinalized));
        Assert.Equal(Ending().Value, snapshot.Logs.Single().Value);
        Assert.Equal(outcome, finalized.Outcome);
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, finalized);
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, scenario.Server.SerializeForWire(snapshot));
        AssertOwnerJournal(ownerQuest);
        Assert.Empty(completions[scenario.ClientB]);
        scenario.ClientB.Call(() => Assert.Null(Campaign.Current.LogEntryHistory.FindLastGameActionLog(
            (JournalLogEntry entry) => entry.RelatedHero == scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId))));
    }

    [Fact]
    public void ClientRequestedCancellation_PreservesItsLocalizedReason()
    {
        var ownerQuest = scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() => ownerQuest.CompleteQuestWithCancel(Ending()));
        environment.FlushCoalescer();
        scenario.Server.Call(() => Assert.Single(quests.CreatedFor(scenario.Server).Single().JournalEntries,
            log => log.LogText.ToString() == Ending().ToString()));
        AssertOwnerJournal(ownerQuest);
    }

    [Fact]
    public void PollThreadSnapshots_AreAppliedBeforeTheEndingAndArchive()
    {
        scenario.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
        var ownerQuest = scenario.QuestOf(scenario.ClientA);
        scenario.Server.Call(() => scenario.QuestOf(scenario.Server).CompleteQuestWithCancel(Ending()));
        environment.FlushCoalescer();
        scenario.ClientA.PumpGameThread();
        scenario.ClientB.PumpGameThread();
        AssertOwnerJournal(ownerQuest);
    }

    [Fact]
    public void Snapshot_ReconcilesDivergentJournalsAndRepeatedTextWithoutDependingOnCounts()
    {
        var quest = scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            quest.AddLog(new TextObject("local progress one"));
            quest.AddLog(new TextObject("local progress two"));
            quest.AddLog(Ending());
        });
        var journal = new NetworkIssueJournal(scenario.NotableId, QuestScenario.IssueId, true,
            new[] { new TextObject("server progress"), Ending(), Ending() });
        var wire = scenario.Server.SerializeForWire(journal);
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, wire);
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, wire);
        scenario.ClientB.SimulateMessage(scenario.Server.NetPeer, wire);
        scenario.ClientA.Call(() =>
        {
            Assert.Equal(5, quest.JournalEntries.Count);
            Assert.Equal(2, quest.JournalEntries.Count(log => log.LogText.ToString() == Ending().ToString()));
        });
        scenario.ClientB.Call(() => Assert.Null(scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnstartedSaveMirror_NeverGetsTheJournalEvenWithALegacyQuestId(bool localOwner)
    {
        var joiner = scenario.ClientB;
        var mirror = scenario.AttachQuest(joiner);
        joiner.Call(() =>
        {
            using (new AllowedThread()) mirror.StringId = null;
            if (localOwner)
            {
                var owners = joiner.Resolve<IIssueOwnershipRegistry>();
                owners.Remove(scenario.NotableId);
                Assert.True(owners.TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerB));
            }
        });
        joiner.SimulateMessage(scenario.Server.NetPeer, scenario.Server.SerializeForWire(
            new NetworkIssueJournal(scenario.NotableId, QuestScenario.IssueId, true, new[] { Ending() })));
        joiner.Call(() => Assert.Empty(mirror.JournalEntries));
    }

    [Fact]
    public void StaleIssueMessages_DoNotTouchTheReplacementOrItsOwner()
    {
        var quest = scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            var giver = scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId);
            using (new AllowedThread()) giver.Issue.StringId = "replacement_issue";
            Assert.True(scenario.ClientA.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                scenario.NotableId, "replacement_issue", QuestScenario.ControllerA));
        });
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, scenario.Server.SerializeForWire(
            new NetworkIssueJournal(scenario.NotableId, QuestScenario.IssueId, true, new[] { Ending() })));
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer,
            new NetworkIssueFinalized(scenario.NotableId, QuestScenario.IssueId, IssueOutcome.QuestCancel));
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsOngoing);
            Assert.Empty(quest.JournalEntries);
            Assert.True(scenario.ClientA.Resolve<IIssueOwnershipRegistry>().TryGetOwner(
                scenario.NotableId, "replacement_issue", out _));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoOwners_KeepTheirOwnQuestIdentityAndArchive(bool legacyIds)
    {
        var firstGiverId = environment.CreateRegisteredObject<Hero>();
        var secondGiverId = environment.CreateRegisteredObject<Hero>();
        var created = new Dictionary<(EnvironmentInstance, string), QuestBase>();
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            foreach (var (giverId, controllerId, localOwner) in new[]
            {
                (firstGiverId, QuestScenario.ControllerA, scenario.ClientA),
                (secondGiverId, QuestScenario.ControllerB, scenario.ClientB),
            })
            {
                instance.Call(() =>
                {
                    using (new AllowedThread())
                    {
                        var giver = instance.GetRegisteredObject<Hero>(giverId);
                        var issue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(giver, null);
                        issue.StringId = giverId + "_issue";
                        issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                        Campaign.Current.IssueManager._issues.Add(giver, issue);
                        giver.OnIssueCreatedForHero(issue);
                        if (instance == scenario.Server || instance == localOwner)
                        {
                            var quest = new UnstartedQuest(legacyIds ? null! : giverId + "_quest", giver);
                            issue.IssueQuest = quest;
                            created[(instance, giverId)] = quest;
                            if (instance == localOwner) quest.StartQuest();
                        }
                    }
                    Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, giverId + "_issue", controllerId));
                });
            }
        }
        var firstQuest = created[(scenario.ClientA, firstGiverId)];
        var secondQuest = created[(scenario.ClientB, secondGiverId)];
        if (!legacyIds) Assert.NotEqual(firstQuest.StringId, secondQuest.StringId);
        scenario.Server.Call(() => created[(scenario.Server, firstGiverId)].CompleteQuestWithCancel(Ending()));
        environment.FlushCoalescer();
        scenario.ClientA.Call(() => Assert.True(firstQuest.IsFinalized));
        scenario.ClientB.Call(() =>
        {
            Assert.True(secondQuest.IsOngoing);
            Assert.Empty(secondQuest.JournalEntries);
        });
        var secondLine = new TextObject("second owner's ending");
        scenario.Server.Call(() => created[(scenario.Server, secondGiverId)].CompleteQuestWithCancel(secondLine));
        environment.FlushCoalescer();
        Assert.Equal(2, scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>().Count());
        foreach (var (instance, quest, giverId, expected, other) in new[]
        {
            (scenario.ClientA, firstQuest, firstGiverId, Ending().ToString(), secondLine.ToString()),
            (scenario.ClientB, secondQuest, secondGiverId, secondLine.ToString(), Ending().ToString()),
        })
        {
            instance.Call(() =>
            {
                Assert.True(quest.IsFinalized);
                Assert.Single(quest.JournalEntries, log => log.LogText.ToString() == expected);
                var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog((JournalLogEntry entry) => entry.IsRelatedTo(quest));
                Assert.NotNull(archive);
                Assert.Same(instance.GetRegisteredObject<Hero>(giverId), archive.RelatedHero);
                Assert.True(archive.IsEnded());
                Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == expected);
                Assert.DoesNotContain(archive.GetEntries(), log => log.LogText.ToString() == other);
            });
        }
    }

    private void AssertOwnerJournal(QuestBase quest)
    {
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Single(completions[scenario.ClientA], ended => ReferenceEquals(ended, quest));
            Assert.Single(quest.JournalEntries, log => log.LogText.ToString() == Ending().ToString());
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog((JournalLogEntry entry) => entry.IsRelatedTo(quest));
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == Ending().ToString());
        });
    }

    private static TextObject Ending() => new TextObject("{=3985-test-ending}Ended for {REASON}: {COUNT}")
        .SetTextVariable("REASON", new TextObject("{=3985-test-reason}army dispersed"))
        .SetTextVariable("COUNT", 7);

    private static void Complete(QuestBase quest, IssueOutcome outcome)
    {
        switch (outcome)
        {
            case IssueOutcome.QuestSuccess: quest.CompleteQuestWithSuccess(); break;
            case IssueOutcome.QuestFail: quest.CompleteQuestWithFail(); break;
            case IssueOutcome.QuestBetrayal: quest.CompleteQuestWithBetrayal(); break;
            case IssueOutcome.QuestCancel: quest.CompleteQuestWithCancel(); break;
            case IssueOutcome.QuestTimeOut: quest.CompleteQuestWithTimeOut(); break;
        }
    }
}
