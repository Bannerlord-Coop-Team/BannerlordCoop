using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Lord Needs Horses goes through the framework's accept like every other quest: each player can hold one, nobody two.
/// </summary>
public class LordNeedsHorsesAcceptTests : IDisposable
{
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const string FirstIssueId = "horse_first";
    private const string SecondIssueId = "horse_second";

    // Only the world requirements of taking the quest are stubbed (relations, faction, mount ratios), the rules under test are real
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));
    private readonly string firstNotableId;
    private readonly string secondNotableId;
    private readonly Player playerA;
    private readonly Player playerB;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public LordNeedsHorsesAcceptTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        firstNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        secondNotableId = TestEnvironment.CreateRegisteredObject<Hero>();

        playerA = CreatePlayer(ControllerA);
        playerB = CreatePlayer(ControllerB);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerA));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerB));
            });

            AddIssue(instance, firstNotableId, FirstIssueId);
            AddIssue(instance, secondNotableId, SecondIssueId);
        }

        Connect(ClientA, playerA);
        Connect(ClientB, playerB);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void EachPlayer_CanHoldAHorseQuestAtTheSameTime()
    {
        Accept(ClientA, firstNotableId);
        Accept(ClientB, secondNotableId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(ControllerA, OwnerOnServer(firstNotableId, FirstIssueId));
        Assert.Equal(ControllerB, OwnerOnServer(secondNotableId, SecondIssueId));
    }

    [Fact]
    public void AnEligiblePlayer_IsNotRefused()
    {
        Accept(ClientA, firstNotableId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal(ControllerA, accepted.ControllerId);
    }

    [Fact]
    public void ThePlayerHoldingAHorseQuest_IsRefusedASecondOne()
    {
        Accept(ClientA, firstNotableId);
        Accept(ClientA, secondNotableId);

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(SecondIssueId, rejected.IssueId);
        Assert.Null(OwnerOnServer(secondNotableId, SecondIssueId));
        Assert.Equal(ControllerA, OwnerOnServer(firstNotableId, FirstIssueId));
    }

    [Fact]
    public void ThePlayerHoldingAHorseQuest_IsRefusedASecondOneEvenWhenAnotherPlayerHoldsOneToo()
    {
        Accept(ClientA, firstNotableId);
        Accept(ClientB, secondNotableId);

        Assert.False(CheckPreconditions(ClientA, secondNotableId));
        Assert.False(CheckPreconditions(ClientB, firstNotableId));
    }

    private Player CreatePlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        return new Player(controllerId, heroId, partyId, "", "");
    }

    private void Connect(EnvironmentInstance client, Player player)
    {
        TestEnvironment.ConnectRegisteredPlayer(client, player.ControllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(player.ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Assert.Same(hero, Hero.MainHero);
        });
    }

    private void AddIssue(EnvironmentInstance instance, string notableId, string issueId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                // The quest's dialogs read encyclopedia links
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                foreach (var player in new[] { playerA, playerB })
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                    hero._partyBelongedTo = party;
                }

                var issue = new Issue(notable);
                issue.StringId = issueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }

    private static void Accept(EnvironmentInstance client, string notableId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            notable.Issue.StartIssueWithQuest();
        });
    }

    private static bool CheckPreconditions(EnvironmentInstance client, string notableId)
    {
        var result = false;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            result = notable.Issue.CheckPreconditions(notable, out _);
        });

        return result;
    }

    private string? OwnerOnServer(string notableId, string issueId)
    {
        string? controllerId = null;

        Server.Call(() =>
        {
            Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, issueId, out controllerId);
        });

        return controllerId;
    }
}
