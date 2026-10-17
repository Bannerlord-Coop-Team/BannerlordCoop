using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.UI.Notifications.Patches;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.UI;

public class ServerQuestNotificationTests : IDisposable
{
    private const string IssueId = "issue_notification";

    private readonly MethodCallRecorder notifications;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public ServerQuestNotificationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        notifications = new MethodCallRecorder(AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
    }

    public void Dispose()
    {
        notifications.Dispose();
        TestEnvironment.Dispose();
    }

    private void RaiseSentTroopsFinished(EnvironmentInstance instance, string notableId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            IssueBase issue;
            using (new AllowedThread())
            {
                issue = new AllowedIssue(notable, null);
                issue.StringId = IssueId;
            }

            new DefaultNotificationsCampaignBehavior().OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest, notable);
        });
    }

    [Fact]
    public void SentTroopsFinished_ShowsNoPopUpOnTheServer()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();

        RaiseSentTroopsFinished(Server, notableId);

        Assert.Equal(0, notifications.CountFor(Server));
    }

    [Fact]
    public void QuestNotifications_AreSkippedOnTheServerAndKeptOnClients()
    {
        Server.Call(() =>
        {
            Assert.False(ServerQuestNotificationPatches.OnIssueUpdatedPrefix());
            Assert.False(ServerQuestNotificationPatches.OnQuestCompletedPrefix());
            Assert.False(ServerQuestNotificationPatches.OnQuestLogAddedPrefix());
            Assert.False(ServerQuestNotificationPatches.OnQuestStartedPrefix());
        });

        Client.Call(() =>
        {
            Assert.True(ServerQuestNotificationPatches.OnIssueUpdatedPrefix());
            Assert.True(ServerQuestNotificationPatches.OnQuestCompletedPrefix());
            Assert.True(ServerQuestNotificationPatches.OnQuestLogAddedPrefix());
            Assert.True(ServerQuestNotificationPatches.OnQuestStartedPrefix());
        });
    }
}
