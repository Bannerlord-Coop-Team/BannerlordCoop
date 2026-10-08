using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

internal sealed class QuestScenario
{
    public const string IssueId = "issue_scenario";
    public const string ControllerA = "player-A";
    public const string ControllerB = "player-B";

    private readonly E2ETestEnvironment testEnvironment;

    public string NotableId { get; }

    public EnvironmentInstance Server => testEnvironment.Server;
    public EnvironmentInstance ClientA => testEnvironment.Clients.First();
    public EnvironmentInstance ClientB => testEnvironment.Clients.Last();

    public QuestScenario(E2ETestEnvironment testEnvironment)
    {
        this.testEnvironment = testEnvironment;

        NotableId = testEnvironment.CreateRegisteredObject<Hero>();

        RegisterPlayer(ClientA, ControllerA);
        RegisterPlayer(ClientB, ControllerB);

        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            AddIssue(instance);
        }
    }

    public void Accept(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));
            notable.Issue.StartIssueWithQuest();
        });
    }

    public QuestBase QuestOf(EnvironmentInstance instance)
    {
        QuestBase quest = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));
            quest = notable.Issue.IssueQuest;
        });

        Assert.NotNull(quest);
        return quest;
    }

    public QuestBase AttachQuest(EnvironmentInstance instance)
    {
        QuestBase quest = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));

            using (new AllowedThread())
            {
                quest = new UnstartedQuest("quest_attached", notable);
                notable.Issue.IssueQuest = quest;
            }
        });

        return quest;
    }

    public string OwnerOnServer()
    {
        var controllerId = string.Empty;

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(NotableId, IssueId, out controllerId));
        });

        return controllerId;
    }

    private void RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));
        testEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
    }

    private void AddIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));

            using (new AllowedThread())
            {
                var issue = new AllowedIssue(notable, null);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }
}
