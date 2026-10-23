using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class SimilarCommitmentsTests : IDisposable
{
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const string FirstIssueId = "issue_first";
    private const string SecondIssueId = "issue_second";

    private readonly StubIssueQuestGenerator quests = new();
    private readonly StubIssuePreconditions preconditions = new(typeof(AllowedIssue));
    private readonly string firstNotableId;
    private readonly string secondNotableId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public SimilarCommitmentsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        firstNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        secondNotableId = TestEnvironment.CreateRegisteredObject<Hero>();

        var playerA = CreatePlayer(ControllerA);
        var playerB = CreatePlayer(ControllerB);

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

        Accept(ClientA, firstNotableId);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        quests.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void AnotherPlayer_IsOfferedTheSameQuestTypeByAnotherGiver()
    {
        Assert.True(CheckPreconditions(ClientB, secondNotableId));

        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(firstNotableId, out var firstNotable));
            Assert.True(firstNotable.Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void ThePlayerHoldingTheQuestType_IsStillRefusedByAnotherGiver()
    {
        Assert.False(CheckPreconditions(ClientA, secondNotableId));
    }

    [Fact]
    public void ThePlayersOwnUnconfirmedAccept_StillCountsAgainstAnotherGiver()
    {
        ClientA.Call(() => ClientA.Resolve<IIssueOwnershipRegistry>().Remove(firstNotableId));

        Assert.False(CheckPreconditions(ClientA, secondNotableId));
    }

    [Fact]
    public void TheGate_PutsOtherPlayersIssuesBackWhenTheCheckThrows()
    {
        preconditions.Throw(ClientB);

        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(firstNotableId, out var firstNotable));
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));

            Assert.Throws<InvalidOperationException>(() => secondNotable.Issue.CheckPreconditions(secondNotable, out _));
            Assert.True(firstNotable.Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void TheServer_AcceptsAnotherPlayersRequestForTheSameQuestType()
    {
        Accept(ClientB, secondNotableId);

        var accepted = Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>()
            .Single(message => message.IssueId == SecondIssueId);
        Assert.Equal(ControllerB, accepted.ControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(ControllerB, OwnerOnServer(secondNotableId, SecondIssueId));
    }

    [Fact]
    public void TheServer_RejectsARequestFromThePlayerHoldingTheQuestType()
    {
        Accept(ClientA, secondNotableId);

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(SecondIssueId, rejected.IssueId);
        Assert.Null(OwnerOnServer(secondNotableId, SecondIssueId));
        Assert.Single(quests.CreatedFor(ClientA), quest => quest.IsFinalized);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));
            Assert.True(secondNotable.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void TheServer_RejectsARequestVanillaRefuses()
    {
        preconditions.Refuse(Server);

        Accept(ClientB, secondNotableId);

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(SecondIssueId, rejected.IssueId);
        Assert.Null(OwnerOnServer(secondNotableId, SecondIssueId));
        Assert.True(Assert.Single(quests.CreatedFor(ClientB)).IsFinalized);
    }

    [Theory]
    [InlineData(IssueBase.PreconditionFlags.NotEnoughTroops)]
    [InlineData(IssueBase.PreconditionFlags.PartySizeLimit)]
    [InlineData(IssueBase.PreconditionFlags.Money)]
    public void TheServer_AcceptsARequestRefusedOnlyForWhatTheAcceptChanges(IssueBase.PreconditionFlags flag)
    {
        preconditions.Refuse(Server, flag);

        Accept(ClientB, secondNotableId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(ControllerB, OwnerOnServer(secondNotableId, SecondIssueId));
    }

    [Fact]
    public void TheServer_RejectsARequestForAnIssueThatCanNoLongerBeSolvedWithoutEndingIt()
    {
        preconditions.FailStayAlive(Server);

        Accept(ClientB, secondNotableId);

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(SecondIssueId, rejected.IssueId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));
            Assert.NotNull(secondNotable.Issue);
            Assert.True(secondNotable.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void AClientCheckingAnIssueThatFailsToStayAlive_IsRefusedWithoutEndingIt()
    {
        preconditions.FailStayAlive(ClientB);

        Assert.False(CheckPreconditions(ClientB, secondNotableId));

        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));
            Assert.NotNull(secondNotable.Issue);
            Assert.True(secondNotable.Issue.IsOngoingWithoutQuest);
            Assert.Same(secondNotable.Issue, Campaign.Current.IssueManager.Issues[secondNotable]);
        });
    }

    [Fact]
    public void TheServerCheckingAnIssueThatFailsToStayAlive_StillEndsIt()
    {
        preconditions.FailStayAlive(Server);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));
            Assert.False(secondNotable.Issue.CheckPreconditions(secondNotable, out _));
            Assert.Null(secondNotable.Issue);
        });
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

    private static void AddIssue(EnvironmentInstance instance, string notableId, string issueId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                var issue = new AllowedIssue(notable, null);
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
