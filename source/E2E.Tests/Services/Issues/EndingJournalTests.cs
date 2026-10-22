using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
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

    public EndingJournalTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        foreach (var instance in environment.Clients.Prepend(environment.Server))
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());
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
            serverQuest.AddLog(Ending());
            Complete(serverQuest, outcome);
        });
        environment.FlushCoalescer();

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(outcome, finalized.Outcome);
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, finalized);
        AssertOwnerJournal(ownerQuest);
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

    private void AssertOwnerJournal(QuestBase quest)
    {
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
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
