using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class QuestEventRelayTests : IDisposable
{
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public QuestEventRelayTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void AClientDoesNotEndARegisteredQuestOutsideTheReplayOfTheServersEnding()
    {
        var quest = scenario.AttachQuest(scenario.ClientA);

        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread())
            {
                quest.CompleteQuestWithCancel();
            }

            Assert.False(quest.IsFinalized);
        });

        Assert.Empty(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Fact]
    public void AClientEndsARegisteredQuestWhenReplayingTheServersEnding()
    {
        var quest = scenario.AttachQuest(scenario.ClientA);

        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread())
            using (new QuestEndingReplay())
            {
                quest.CompleteQuestWithCancel();
            }

            Assert.True(quest.IsFinalized);
        });
    }

    [Fact]
    public void AQuestWithoutListenersIsLeftAloneWhenTheServerRelaysWorldEvents()
    {
        var quest = scenario.AttachQuest(scenario.Server);
        scenario.Server.Call(() => Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA)));

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));

            CampaignEventDispatcher.Instance.OnWarDeclared(notable.MapFaction, notable.MapFaction, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
            CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(null, notable);
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(null, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        });

        Assert.False(quest.IsFinalized);
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
