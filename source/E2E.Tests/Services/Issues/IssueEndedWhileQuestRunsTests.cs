using Common.Messaging;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssueEndedWhileQuestRunsTests : IDisposable
{
    private readonly StubIssueQuestGenerator quests = new();
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public IssueEndedWhileQuestRunsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        quests.Dispose();
    }

    [Fact]
    public void AnIssueEndedWhileItsQuestRunsIsAnnouncedAndClearedEverywhere()
    {
        scenario.Accept(scenario.ClientA);

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            notable.Issue.CompleteIssueWithStayAliveConditionsFailed();
        });

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.IssueStayAliveFailed, finalized.Outcome);

        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
                Assert.Null(notable.Issue);
            });
        }

        scenario.Server.Call(() =>
        {
            Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
        });
    }

    [Fact]
    public void AQuestTimeoutIsAnnouncedOnce()
    {
        scenario.Accept(scenario.ClientA);
        var quest = scenario.QuestOf(scenario.ClientA);

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(quest, IssueOutcome.QuestTimeOut)));

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestTimeOut, finalized.Outcome);
    }
}
