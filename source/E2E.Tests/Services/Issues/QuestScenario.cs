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

    public static readonly Type IssueType = typeof(AllowedIssue);

    private readonly E2ETestEnvironment testEnvironment;

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
            AddIssue(instance, NotableId, IssueId, createIssue);
        }
    }

    // Gives another hero an issue nobody has taken, on every machine
    public void AddIssue(string heroId, string issueId)
    {
        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            AddIssue(instance, heroId, issueId, null);
        }
    }

    public QuestScenario(E2ETestEnvironment testEnvironment, Func<Hero, IssueBase> issueFactory)
        : this(testEnvironment, (_, hero) => issueFactory(hero))
    {
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

    public QuestBase AttachQuest(EnvironmentInstance instance, Func<Hero, QuestBase>? createQuest = null)
    {
        QuestBase quest = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(NotableId, out var notable));

            using (new AllowedThread())
            {
                quest = createQuest == null ? new UnstartedQuest("quest_attached", notable) : createQuest(notable);
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

        var player = new Player(controllerId, heroId, partyId, "", "");
        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(player)));
        testEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        return player;
    }

    private static void AddIssue(EnvironmentInstance instance, string heroId, string issueId,
        Func<EnvironmentInstance, Hero, IssueBase>? createIssue)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            using (new AllowedThread())
            {
                var issue = createIssue == null ? new AllowedIssue(hero, null) : createIssue(instance, hero);
                issue.StringId = issueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(hero, issue);
                hero.OnIssueCreatedForHero(issue);
            }
        });
    }
}
