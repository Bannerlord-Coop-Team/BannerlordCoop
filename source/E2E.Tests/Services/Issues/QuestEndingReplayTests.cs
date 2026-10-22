using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class QuestEndingReplayTests : IDisposable
{
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public QuestEndingReplayTests(ITestOutputHelper output)
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

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DiscardedQuestCopiesRemoveListenersWithoutRunningGameplayFinalization(bool inQuestManager, bool owner)
    {
        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            var issue = giver.Issue;
            var quest = new FinalizationProbeQuest(giver);
            issue.IssueQuest = quest;
            if (inQuestManager) Campaign.Current.QuestManager._quests.Add(quest);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(quest, quest.OnHour);
            Assert.True(scenario.ClientA.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                scenario.NotableId, QuestScenario.IssueId, owner ? QuestScenario.ControllerA : QuestScenario.ControllerB));

            scenario.ClientA.Resolve<IMessageBroker>().Publish(this,
                new NetworkIssueFinalized(scenario.NotableId, QuestScenario.IssueId, IssueOutcome.QuestCancel));

            Assert.Null(giver.Issue);
            Assert.True(quest.IsFinalized);
            Assert.Equal(owner && inQuestManager, quest.GameplayFinalized);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            CampaignEventDispatcher.Instance.OnHourlyTick();
            Assert.Equal(0, quest.HourlyCalls);
        });
    }

    private sealed class FinalizationProbeQuest : UnstartedQuest
    {
        public bool GameplayFinalized { get; private set; }
        public int HourlyCalls { get; private set; }

        public FinalizationProbeQuest(Hero giver) : base("finalization_probe", giver)
        {
        }

        public void OnHour()
        {
            HourlyCalls++;
        }

        protected override void OnFinalize()
        {
            GameplayFinalized = true;
        }
    }
}
