using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Registry.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using UnregisteredIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

namespace E2E.Tests.Services.Issues;

public class ActiveIssueDismisserTests : IDisposable
{
    private const string IssueId = "issue_dismiss";
    private const string ControllerId = "player-A";

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();

    public ActiveIssueDismisserTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static IssueBase AddIssue(EnvironmentInstance instance, string notableId, IssueBase.IssueState state, bool registeredType = true)
    {
        IssueBase issue = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                issue = registeredType ? new AllowedIssue(notable, null) : new UnregisteredIssue(notable);
                issue.StringId = IssueId;
                issue._issueState = state;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });

        return issue;
    }

    private static bool HasIssue(EnvironmentInstance instance, string notableId)
    {
        var hasIssue = false;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            hasIssue = notable.Issue != null;
        });

        return hasIssue;
    }

    private string RegisterPlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));

        return heroId;
    }

    private void SetOwner(string notableId, string controllerId)
    {
        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(notableId, IssueId, controllerId)));
    }

    private bool HasOwner(string notableId)
    {
        var hasOwner = false;

        Server.Call(() => hasOwner = Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _));

        return hasOwner;
    }

    private void PublishServerStarted(EnvironmentInstance instance)
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered()));
    }

    private void PublishDisconnected(EnvironmentInstance instance, EnvironmentInstance leaver)
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(leaver.NetPeer, default(DisconnectInfo))));
    }

    [Fact]
    public void ServerStart_DismissesAQuestIssueNobodyOwns()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);

        PublishServerStarted(Server);

        Assert.False(HasIssue(Server, notableId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void ServerStart_DismissesATroopsIssueNobodyOwnsAndFreesTheCompanion()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var issue = AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithAlternativeSolution);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));

            using (new AllowedThread())
            {
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                companion.ChangeState(Hero.CharacterStates.Disabled);
            }

            Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
        });

        PublishServerStarted(Server);

        Assert.False(HasIssue(Server, notableId));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
        });
    }

    [Fact]
    public void ServerStart_PutsTheCompanionOfATroopsIssueInTheNotablesSettlement()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var issue = AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithAlternativeSolution);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                notable.StayingInSettlement = settlement;
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                companion.ChangeState(Hero.CharacterStates.Disabled);
            }

            Assert.Null(companion.CurrentSettlement);
        });

        PublishServerStarted(Server);

        Assert.False(HasIssue(Server, notableId));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Same(settlement, companion.CurrentSettlement);
        });
    }

    [Fact]
    public void ServerStart_KeepsIdleIssuesAndIssuesOfUnregisteredTypes()
    {
        var idleNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var unregisteredNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Server, idleNotableId, IssueBase.IssueState.Ongoing);
        AddIssue(Server, unregisteredNotableId, IssueBase.IssueState.SolvingWithQuestSolution, registeredType: false);

        PublishServerStarted(Server);

        Assert.True(HasIssue(Server, idleNotableId));
        Assert.True(HasIssue(Server, unregisteredNotableId));
    }

    [Fact]
    public void ServerStart_KeepsAQuestIssueWhoseOwnerIsConnected()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        SetOwner(notableId, ControllerId);

        PublishServerStarted(Server);

        Assert.True(HasIssue(Server, notableId));
        Assert.True(HasOwner(notableId));
    }

    [Fact]
    public void PlayerDisconnected_DismissesTheQuestOfAnOwnerThatLeftAndEveryClientEndsIt()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        AddIssue(Client, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        AddIssue(OtherClient, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        SetOwner(notableId, ControllerId);

        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(Client.NetPeer));
        PublishDisconnected(Server, Client);

        Assert.False(HasIssue(Server, notableId));
        Assert.False(HasIssue(Client, notableId));
        Assert.False(HasIssue(OtherClient, notableId));
        Assert.False(HasOwner(notableId));

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(notableId, finalized.IssueOwnerId);
        Assert.Equal(IssueId, finalized.IssueId);
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
    }

    [Fact]
    public void PlayerDisconnected_KeepsTheQuestOfAnOwnerThatIsStillConnected()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        SetOwner(notableId, ControllerId);

        PublishDisconnected(Server, OtherClient);

        Assert.True(HasIssue(Server, notableId));
        Assert.True(HasOwner(notableId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void PlayerDisconnected_KeepsATroopsIssueOfAnOwnerThatLeft()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithAlternativeSolution);
        SetOwner(notableId, ControllerId);

        PublishDisconnected(Server, Client);

        Assert.True(HasIssue(Server, notableId));
        Assert.True(HasOwner(notableId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void Clients_IgnoreBothEvents()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Client, notableId, IssueBase.IssueState.SolvingWithQuestSolution);

        PublishServerStarted(Client);
        PublishDisconnected(Client, OtherClient);

        Assert.True(HasIssue(Client, notableId));
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
