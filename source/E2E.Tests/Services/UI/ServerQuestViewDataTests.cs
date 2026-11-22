using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.UI;

public class ServerQuestViewDataTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public ServerQuestViewDataTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private int UnreadLogsAfterAnIssueLog(EnvironmentInstance instance, string notableId)
    {
        var unread = -1;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            AllowedIssue issue;
            using (new AllowedThread())
            {
                issue = new AllowedIssue(notable, null);
                issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("Troops sent")));
            }

            var tracker = new ViewDataTrackerCampaignBehavior();
            tracker.OnIssueLogAdded(issue, false);
            unread = tracker._unExaminedQuestLogs.Count;
        });

        return unread;
    }

    private int UnreadLogsAfterAQuestLog(EnvironmentInstance instance, string notableId)
    {
        var unread = -1;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            UnstartedQuest quest;
            using (new AllowedThread())
            {
                quest = new UnstartedQuest("quest_view_data", notable);
                quest.AddLog(new TextObject("Quest updated"));
            }

            var tracker = new ViewDataTrackerCampaignBehavior();
            tracker.OnQuestLogAdded(quest, false);
            unread = tracker._unExaminedQuestLogs.Count;
        });

        return unread;
    }

    [Fact]
    public void AnIssueLogIsNotMarkedUnreadOnTheServerButIsOnClients()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();

        Assert.Equal(0, UnreadLogsAfterAnIssueLog(Server, notableId));
        Assert.Equal(1, UnreadLogsAfterAnIssueLog(Client, notableId));
    }

    [Fact]
    public void AQuestLogIsNotMarkedUnreadOnTheServerButIsOnClients()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();

        Assert.Equal(0, UnreadLogsAfterAQuestLog(Server, notableId));
        Assert.Equal(1, UnreadLogsAfterAQuestLog(Client, notableId));
    }
}
