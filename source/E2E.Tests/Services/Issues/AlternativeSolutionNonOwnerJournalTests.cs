using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class AlternativeSolutionNonOwnerJournalTests : IDisposable
{
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public AlternativeSolutionNonOwnerJournalTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void AClientThatDidNotStartTheMission_HasAJournalEntryForTheDailyTick()
    {
        AcceptedOn(scenario.ClientB, QuestScenario.ControllerA);

        scenario.ClientB.Call(() =>
        {
            Assert.True(scenario.ClientB.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));

            Assert.True(notable.Issue.IsSolvingWithAlternative);
            Assert.Single(notable.Issue.JournalEntries);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(notable.Issue.IsSolvingWithAlternative);
        });
    }

    private void AcceptedOn(EnvironmentInstance client, string ownerControllerId)
    {
        var state = new AlternativeSolutionVanillaState(
            1f,
            0f,
            0,
            string.Empty,
            0f,
            CampaignTime.DaysFromNow(3f).NumTicks,
            CampaignTime.DaysFromNow(2f).NumTicks,
            CampaignTime.DaysFromNow(3f).NumTicks);

        client.Call(() =>
        {
            client.Resolve<IMessageBroker>().Publish(this, new NetworkAlternativeSolutionAccepted(
                scenario.NotableId,
                QuestScenario.IssueId,
                Array.Empty<TroopRosterElementData>(),
                state,
                Array.Empty<byte>(),
                ownerControllerId));
        });

        TestEnvironment.FlushCoalescer();
    }
}
