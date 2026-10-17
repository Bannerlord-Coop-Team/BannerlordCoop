using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class QuestSolutionAcceptRaceTests : IDisposable
{
    private readonly StubIssueQuestGenerator quests = new();
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public QuestSolutionAcceptRaceTests(ITestOutputHelper output)
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
    public void TheFirstPlayerToAcceptOwnsTheIssueAndTheServerKeepsItsQuest()
    {
        scenario.Accept(scenario.ClientA);

        var accepted = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, accepted.ControllerId);
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(notable.Issue.IsSolvingWithQuest);
            Assert.NotNull(notable.Issue.IssueQuest);
        });

        scenario.ClientB.Call(() =>
        {
            Assert.True(scenario.ClientB.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(notable.Issue.IsSolvingWithQuest);
            Assert.Null(notable.Issue.IssueQuest);
        });
    }

    [Fact]
    public void TheSecondPlayerToAcceptIsRejectedAndItsStrayQuestIsCancelledWithoutEndingTheIssue()
    {
        scenario.Accept(scenario.ClientA);
        scenario.Accept(scenario.ClientB);

        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());

        var strayQuest = Assert.Single(quests.CreatedFor(scenario.ClientB));

        scenario.ClientB.Call(() =>
        {
            Assert.True(scenario.ClientB.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsSolvingWithQuest);
            Assert.Null(notable.Issue.IssueQuest);
            Assert.True(strayQuest.IsFinalized);
        });

        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.NotNull(notable.Issue.IssueQuest);
            Assert.True(notable.Issue.IssueQuest.IsOngoing);
        });

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsSolvingWithQuest);
        });
    }
}
