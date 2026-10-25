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
using System.Reflection;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

internal sealed class QuestScenario
{
    public const string IssueId = "issue_scenario";
    public const string ControllerA = "player-A";
    public const string ControllerB = "player-B";

    public static readonly Type IssueType = typeof(AllowedIssue);

    private readonly E2ETestEnvironment testEnvironment;
    private readonly Action<EnvironmentInstance, Hero>? prepare;

    public string NotableId { get; }
    public Player PlayerA { get; }
    public Player PlayerB { get; }
    public string OwnerHeroId => PlayerA.HeroId;
    public string OwnerPartyId => PlayerA.MobilePartyId;
    public string OtherHeroId => PlayerB.HeroId;
    public string OtherPartyId => PlayerB.MobilePartyId;

    public EnvironmentInstance Server => testEnvironment.Server;
    public EnvironmentInstance ClientA => testEnvironment.Clients.First();
    public EnvironmentInstance ClientB => testEnvironment.Clients.Last();

    public QuestScenario(E2ETestEnvironment testEnvironment,
        Func<EnvironmentInstance, Hero, IssueBase>? createIssue = null)
    {
        this.testEnvironment = testEnvironment;

        NotableId = testEnvironment.CreateRegisteredObject<Hero>();

        PlayerA = RegisterPlayer(ClientA, ControllerA);
        PlayerB = RegisterPlayer(ClientB, ControllerB);

        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            AddIssue(instance, createIssue);
        }
    }

    public QuestScenario(E2ETestEnvironment testEnvironment, Func<Hero, IssueBase>? createIssue,
        Action<EnvironmentInstance, Hero>? prepare)
    {
        this.testEnvironment = testEnvironment;
        Func<EnvironmentInstance, Hero, IssueBase>? issueFactory = createIssue == null ? null : (_, hero) => createIssue(hero);
        this.prepare = prepare;

        NotableId = testEnvironment.CreateRegisteredObject<Hero>();
        PlayerA = RegisterPlayer(ClientA, ControllerA);
        PlayerB = RegisterPlayer(ClientB, ControllerB);

        foreach (var instance in new[] { Server, ClientA, ClientB }) AddIssue(instance, issueFactory);
    }

    public void Accept(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));
            notable.Issue.StartIssueWithQuest();
        });
    }

    public void Accept(EnvironmentInstance client, IEnumerable<MethodBase>? disabledMethods) =>
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));
            notable.Issue.StartIssueWithQuest();
        }, disabledMethods);

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

    private Player RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();

        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread()) hero._partyBelongedTo = party;
            });
        }

        var player = new Player(controllerId, heroId, partyId, "", "");
        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(player)));
        testEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        return player;
    }

    private void AddIssue(EnvironmentInstance instance, Func<EnvironmentInstance, Hero, IssueBase>? createIssue)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));

            using (new AllowedThread())
            {
                var issue = createIssue == null ? new AllowedIssue(notable, null) : createIssue(instance, notable);
                prepare?.Invoke(instance, notable);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }
}
