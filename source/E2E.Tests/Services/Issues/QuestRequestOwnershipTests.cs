using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class QuestRequestOwnershipTests : IDisposable
{
    private const byte Branch = (byte)GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods;

    private readonly StubIssueQuestGenerator quests = new();
    private readonly MethodCallRecorder branches;
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public QuestRequestOwnershipTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
        branches = new MethodCallRecorder(AccessTools.Method(
            typeof(GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy),
            nameof(GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy.TryRunBranch)));
    }

    public void Dispose()
    {
        branches.Dispose();
        TestEnvironment.Dispose();
        quests.Dispose();
    }

    [Fact]
    public void AnOutcomeRequestFromTheOwnerEndsTheIssueEverywhere()
    {
        scenario.Accept(scenario.ClientA);
        var quest = scenario.QuestOf(scenario.ClientA);

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(quest, IssueOutcome.QuestCancel)));

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);

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
    public void AnOutcomeRequestFromAPlayerWhoDoesNotOwnTheIssueIsIgnored()
    {
        scenario.Accept(scenario.ClientA);
        var mirrorQuest = scenario.QuestOf(scenario.Server);
        var foreignQuest = scenario.AttachQuest(scenario.ClientB);

        scenario.ClientB.Call(() => scenario.ClientB.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(foreignQuest, IssueOutcome.QuestCancel)));

        Assert.Single(scenario.ClientB.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsSolvingWithQuest);
            Assert.True(mirrorQuest.IsOngoing);
        });
    }

    [Fact]
    public void AnOutcomeRequestForAnOwnedIssueThatHasNoQuestChangesNothing()
    {
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));

            using (new AllowedThread())
            {
                notable.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            }
        });
        var attachedQuest = scenario.AttachQuest(scenario.ClientA);

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(attachedQuest, IssueOutcome.QuestCancel)));

        Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsSolvingWithAlternative);
        });
    }

    [Fact]
    public void ABranchRequestFromTheOwnerRunsTheBranchOnTheServer()
    {
        scenario.Accept(scenario.ClientA);
        var quest = scenario.QuestOf(scenario.ClientA);

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestBranchRequested(quest, Branch)));

        Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal(1, branches.CountFor(scenario.Server));
    }

    [Fact]
    public void ABranchRequestFromAPlayerWhoDoesNotOwnTheIssueIsIgnored()
    {
        scenario.Accept(scenario.ClientA);
        var foreignQuest = scenario.AttachQuest(scenario.ClientB);

        scenario.ClientB.Call(() => scenario.ClientB.Resolve<IMessageBroker>().Publish(this, new QuestBranchRequested(foreignQuest, Branch)));

        Assert.Single(scenario.ClientB.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal(0, branches.CountFor(scenario.Server));
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }
}
