using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Finalization;
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

    [Fact]
    public void AClientDoesNotEndAStaleRegisteredIssueOutsideTheReplayOfTheServersEnding()
    {
        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            var issue = notable.Issue;

            using (new AllowedThread())
            {
                issue.CompleteIssueWithStayAliveConditionsFailed();
            }

            Assert.False(issue._areIssueEffectsResolved);
        });
    }

    [Fact]
    public void AClientEndsAStaleRegisteredIssueWhenReplayingTheServersEnding()
    {
        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            var issue = notable.Issue;

            using (new AllowedThread())
            using (new QuestEndingReplay())
            {
                issue.CompleteIssueWithStayAliveConditionsFailed();
            }

            Assert.True(issue._areIssueEffectsResolved);
        });
    }

    [Fact]
    public void TheServerEndsAStaleIssueWithoutAQuestAndEveryMachineClearsIt()
    {
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
    }
}
