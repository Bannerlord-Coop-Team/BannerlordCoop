using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssueFinishedByAiLordTests : IDisposable
{
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public IssueFinishedByAiLordTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void AnIssueFinishedByAnAiLordOnTheServerIsClearedOnEveryClient()
    {
        var aiLordId = TestEnvironment.CreateRegisteredObject<Hero>();

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(aiLordId, out var aiLord));
            notable.Issue.CompleteIssueWithAiLord(aiLord);
        });

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.IssueFinishedByAiLord, finalized.Outcome);
        Assert.Equal(QuestScenario.IssueId, finalized.IssueId);

        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
                Assert.Null(notable.Issue);
            });
        }
    }

    [Fact]
    public void AClientDoesNotFinishAnIssueWithAnAiLordOnItsOwn()
    {
        var aiLordId = TestEnvironment.CreateRegisteredObject<Hero>();

        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(aiLordId, out var aiLord));
            notable.Issue.CompleteIssueWithAiLord(aiLord);
            Assert.NotNull(notable.Issue);
        });

        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
