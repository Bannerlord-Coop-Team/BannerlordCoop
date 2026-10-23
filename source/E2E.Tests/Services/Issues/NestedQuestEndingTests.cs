using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Preserves vanilla's last completion detail across nested endings and serialized replay.</summary>
public class NestedQuestEndingTests : IDisposable
{
    private readonly StubIssueQuestGenerator generator = new();
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly Dictionary<EnvironmentInstance, List<(QuestBase Quest, QuestBase.QuestCompleteDetails Detail)>> completions = new();
    private readonly NestedTimeoutQuest serverQuest;
    private readonly NestedTimeoutQuest ownerQuest;

    public NestedQuestEndingTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        scenario.Accept(scenario.ClientA);
        foreach (var instance in environment.Clients.Prepend(scenario.Server))
        {
            var completed = completions[instance] = new();
            instance.Call(() =>
            {
                new JournalLogsCampaignBehavior().RegisterEvents();
                CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(completed, (quest, detail) => completed.Add((quest, detail)));
            });
        }
        serverQuest = InstallQuest(scenario.Server);
        ownerQuest = InstallQuest(scenario.ClientA);
    }

    public void Dispose()
    {
        environment.Dispose();
        preconditions.Dispose();
        generator.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnersHourlyTick_NestedFailureAnnouncesTheVanillaTimeoutOnce(bool pollerThread)
    {
        if (pollerThread) scenario.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
        TickOwner();
        TickOwner();

        var request = Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Equal(IssueOutcome.QuestTimeOut, request.Outcome);
        Assert.Equal(new[] { QuestBase.QuestCompleteDetails.Fail, QuestBase.QuestCompleteDetails.Timeout },
            completions[scenario.Server].Select(completed => completed.Detail));
        Assert.Equal(1, serverQuest.TimeoutCalls);
        Assert.Equal(1, serverQuest.FailureCalls);
        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestTimeOut, finalized.Outcome);
        var journal = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        AssertOwnerEnding(scenario.ClientA, ownerQuest, QuestBase.QuestCompleteDetails.Timeout, TimeoutText().ToString());

        // Retry the serialized request and both received messages after completion.
        scenario.Server.SimulateMessage(scenario.ClientA.NetPeer, scenario.ClientA.SerializeForWire(request));
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, scenario.Server.SerializeForWire(journal));
        scenario.ClientA.SimulateMessage(scenario.Server.NetPeer, scenario.Server.SerializeForWire(finalized));
        Pump();
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        Assert.Equal(1, serverQuest.TimeoutCalls);
        Assert.Equal(0, ownerQuest.TimeoutCalls);
        Assert.Equal(0, ownerQuest.FailureCalls);
        AssertOwnerEnding(scenario.ClientA, ownerQuest, QuestBase.QuestCompleteDetails.Timeout, TimeoutText().ToString());
        Assert.Empty(completions[scenario.ClientB]);
    }

    [Fact]
    public void TimeoutRedirectedToSuccess_AnnouncesTheActualVanillaSuccess()
    {
        serverQuest.Behavior = TimeoutBehavior.Success;
        TickOwner();
        Assert.Equal(QuestBase.QuestCompleteDetails.Success, Assert.Single(completions[scenario.Server]).Detail);
        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);
        Assert.Equal(0, serverQuest.TimeoutCalls);
        Assert.Equal(1, serverQuest.SuccessCalls);
        Assert.Equal(0, ownerQuest.SuccessCalls);
        AssertOwnerEnding(scenario.ClientA, ownerQuest, QuestBase.QuestCompleteDetails.Success, SuccessText().ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredOrThrowingTimeout_DoesNotSuppressALaterCancellation(bool throws)
    {
        serverQuest.Behavior = throws ? TimeoutBehavior.Throw : TimeoutBehavior.Defer;
        TickOwner();
        Assert.True(serverQuest.IsOngoing);
        Assert.True(ownerQuest.IsOngoing);
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Empty(completions[scenario.Server]);
        scenario.ClientA.Call(() => ownerQuest.CompleteQuestWithCancel(CancelText()));
        Pump();
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        AssertOwnerEnding(scenario.ClientA, ownerQuest, QuestBase.QuestCompleteDetails.Cancel, CancelText().ToString());
    }

    [Fact]
    public void AnotherOwnersCancellationInsideTimeout_RemainsAnIndependentEnding()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        NestedTimeoutQuest otherServer = null!;
        NestedTimeoutQuest otherOwner = null!;
        foreach (var instance in environment.Clients.Prepend(scenario.Server))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = instance.GetRegisteredObject<Hero>(giverId);
                    var issue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(giver, null);
                    issue.StringId = "independent_issue";
                    issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                    if (instance != scenario.ClientA)
                    {
                        var quest = new NestedTimeoutQuest("independent_quest", giver);
                        issue.IssueQuest = quest;
                        if (instance == scenario.Server) otherServer = quest;
                        else
                        {
                            otherOwner = quest;
                            quest.StartQuest();
                        }
                    }
                }
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, "independent_issue", QuestScenario.ControllerB));
            });
        }
        serverQuest.DuringTimeout = () => otherServer.CompleteQuestWithCancel(CancelText());
        TickOwner();
        var finalized = scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>().ToArray();
        Assert.Equal(2, finalized.Length);
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(finalized, message => message.IssueId == "independent_issue").Outcome);
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(finalized, message => message.IssueId == QuestScenario.IssueId).Outcome);
        AssertOwnerEnding(scenario.ClientA, ownerQuest, QuestBase.QuestCompleteDetails.Timeout, TimeoutText().ToString());
        AssertOwnerEnding(scenario.ClientB, otherOwner, QuestBase.QuestCompleteDetails.Cancel, CancelText().ToString());
        Assert.DoesNotContain(ownerQuest.JournalEntries, log => log.LogText.ToString() == CancelText().ToString());
        Assert.DoesNotContain(otherOwner.JournalEntries, log => log.LogText.ToString() == TimeoutText().ToString());
    }

    private NestedTimeoutQuest InstallQuest(EnvironmentInstance instance)
    {
        NestedTimeoutQuest quest = null!;
        instance.Call(() =>
        {
            using (new AllowedThread())
            {
                var giver = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                quest = new NestedTimeoutQuest("nested_timeout_quest", giver);
                giver.Issue.IssueQuest = quest;
                if (instance != scenario.Server) quest.StartQuest();
                quest.QuestDueTime = CampaignTime.Now - CampaignTime.Hours(1);
            }
        });
        return quest;
    }

    private void TickOwner()
    {
        scenario.ClientA.Call(() => Campaign.Current.QuestManager.HourlyTick());
        Pump();
    }

    private void Pump()
    {
        environment.FlushCoalescer();
        scenario.Server.PumpGameThread();
        foreach (var client in environment.Clients) client.PumpGameThread();
        environment.FlushCoalescer();
    }

    private void AssertOwnerEnding(EnvironmentInstance instance, QuestBase quest, QuestBase.QuestCompleteDetails detail, string text)
    {
        instance.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Equal(detail, Assert.Single(completions[instance], completed => ReferenceEquals(completed.Quest, quest)).Detail);
            Assert.Single(quest.JournalEntries, log => log.LogText.ToString() == text);
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog((JournalLogEntry entry) => entry.IsRelatedTo(quest));
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            Assert.Equal(detail, archive._questCompletionDetail);
            Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == text);
        });
    }

    private static TextObject TimeoutText() => new TextObject("{=3985-nested-timeout}The task expired for {REASON}.")
        .SetTextVariable("REASON", new TextObject("nested timeout"));
    private static TextObject SuccessText() => new TextObject("{=3985-nested-success}The task succeeded before its deadline.");
    private static TextObject CancelText() => new TextObject("{=3985-independent-cancel}An independent agreement ended.");

    private enum TimeoutBehavior { Failure, Success, Defer, Throw }

    /// <summary>Models vanilla's nested failure and its explicit before-timeout redirect flags.</summary>
    private sealed class NestedTimeoutQuest : UnstartedQuest
    {
        public TimeoutBehavior Behavior;
        public Action? DuringTimeout;
        public int TimeoutCalls;
        public int FailureCalls;
        public int SuccessCalls;

        public NestedTimeoutQuest(string id, Hero giver) : base(id, giver) { }

        public override void OnBeforeTimedOut(ref bool completeWithSuccess, ref bool doNotResolveTheQuest)
        {
            if (ModInformation.IsClient) return;
            completeWithSuccess = Behavior == TimeoutBehavior.Success;
            doNotResolveTheQuest = Behavior == TimeoutBehavior.Defer;
        }

        public override void OnTimedOut()
        {
            if (ModInformation.IsClient) return;
            if (Behavior == TimeoutBehavior.Throw) throw new InvalidOperationException("timeout regression fixture");
            TimeoutCalls++;
            AddLog(TimeoutText());
            DuringTimeout?.Invoke();
            CompleteQuestWithFail();
        }

        public override void OnFailed()
        {
            if (ModInformation.IsServer) FailureCalls++;
        }

        public override void OnCompleteWithSuccess()
        {
            if (ModInformation.IsClient) return;
            SuccessCalls++;
            AddLog(SuccessText());
        }
    }
}
