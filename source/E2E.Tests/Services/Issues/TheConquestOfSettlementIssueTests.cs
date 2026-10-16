using Common.Util;
using E2E.Tests.Environment;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Conversation;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Quests.TheConquestOfSettlement;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

public sealed class TheConquestOfSettlementIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly string issueOwnerId;
    private readonly string targetSettlementId;

    private EnvironmentInstance Server => environment.Server;

    public TheConquestOfSettlementIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        issueOwnerId = environment.CreateRegisteredObject<Hero>();
        targetSettlementId = environment.CreateRegisteredObject<Settlement>();
    }

    public void Dispose() => environment.Dispose();

    private void CreateIssue()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));

            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, issueOwner));
        });
    }

    [Fact]
    public void ServerCreationUsesCapturedSettlementAndIdentityOnEveryClient()
    {
        Server.Call(() => Campaign.Current.IssueManager._nextIssueUniqueIndex = 41);
        foreach (var client in environment.Clients)
            client.Call(() => Campaign.Current.IssueManager._nextIssueUniqueIndex = 7);
        CreateIssue();

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.Equal(issueOwnerId, created.IssueOwnerId);
        Assert.Equal(typeof(Issue).Name, created.IssueTypeName);
        Assert.False(string.IsNullOrEmpty(created.IssueId));
        Assert.Equal("issue_41", created.IssueId);

        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));
                var issue = Assert.IsType<Issue>(issueOwner.Issue);

                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Same(target, issue._targetSettlement);
            });
        }
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("other", false)]
    [InlineData("none", false)]
    public void RoamingGiverRequiresThisPlayersEngagement(string engagedPlayer, bool allowed)
    {
        var owner = environment.Clients.First();
        var other = environment.Clients.Last();
        var ownerPartyId = RegisterPlayer(owner, "owner");
        var otherPartyId = RegisterPlayer(other, "other");
        var giverPartyId = PlaceGiverOnMap();
        CreateIssue();

        if (engagedPlayer != "none")
            BeginEngagement(engagedPlayer == "owner" ? owner : other,
                engagedPlayer == "owner" ? ownerPartyId : otherPartyId, giverPartyId);

        owner.Call(() => owner.Resolve<IMessageBroker>().Publish(this, new IssueConversationOpenedLocally(issueOwnerId)));

        Assert.Equal(allowed ? 1 : 0, Server.NetworkSentMessages.GetMessages<NetworkIssueConversationAllowed>().Count());
        Assert.Equal(allowed ? 0 : 1, Server.NetworkSentMessages.GetMessages<NetworkIssueConversationDenied>().Count());
        foreach (var instance in new[] { Server, owner, other })
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));
                Assert.Equal(allowed && instance != other,
                    instance.Resolve<IIssueConversationTracker>().TryGet(issueOwnerId, "owner", giver.Issue.StringId));
            });
    }

    [Fact]
    public void AcceptedMapOfferCreatesOnlyTheOwnersQuestAndServerMirror()
    {
        var owner = environment.Clients.First();
        var other = environment.Clients.Last();
        AcceptMapOffer();

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal("owner", accepted.ControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        foreach (var instance in new[] { Server, owner, other })
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(issueOwnerId, giver.Issue.StringId, out var controller));
                Assert.Equal("owner", controller);
                Assert.True(giver.Issue.IsSolvingWithQuest);
                if (instance == other) Assert.Null(giver.Issue.IssueQuest);
                else
                {
                    var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                    Assert.True(quest.IsOngoing);
                    Assert.Single(quest.JournalEntries);
                }
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SiegeCompletionRewardsOnlyTheOwnerAndOnlyAfterVictory(bool victory)
    {
        AcceptMapOffer();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer("owner", out var player));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer("other", out var observer));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(observer.HeroId, out var otherHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var ownerParty));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            using (new AllowedThread())
            {
                ownerParty.ActualClan = giver.Clan;
                ownerHero.Clan = giver.Clan;
            }
            var ownerGold = ownerHero.Gold;
            var otherGold = otherHero.Gold;

            CampaignEventDispatcher.Instance.SiegeCompleted(target, ownerParty, victory, MapEvent.BattleTypes.Siege);

            Assert.Equal(victory ? ownerGold + quest.RewardGold : ownerGold, ownerHero.Gold);
            Assert.Equal(otherGold, otherHero.Gold);
            Assert.Equal(victory, quest.IsFinalized);
        });

        if (victory)
            Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        else
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("other", false)]
    public void HostilityKeepsTheActualInitiatorWhenTheQuestOwnerIsSubstituted(string initiator, bool causedByOwner)
    {
        AcceptMapOffer();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(initiator, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            var patch = typeof(TheConquestOfSettlementEventPatches);
            using (new MainHeroSubstitutionScope(hero, party))
            {
                var arguments = new object?[] { quest, null };
                Assert.True((bool)AccessTools.Method(patch, "Prefix").Invoke(null, arguments)!);
                try
                {
                    Assert.Same(hero, TheConquestOfSettlementEventPatches.TriggeringHero);
                    Assert.Equal(causedByOwner, DiplomacyHelper.IsWarCausedByPlayer(null, null,
                        DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility));
                }
                finally
                {
                    AccessTools.Method(patch, "Finalizer").Invoke(null, new[] { arguments[1] });
                }
                Assert.Same(hero, ResolvedMainHeroContext.ResolvedMainHero);
            }
        });
    }

    private void AcceptMapOffer()
    {
        var owner = environment.Clients.First();
        var other = environment.Clients.Last();
        var ownerPartyId = RegisterPlayer(owner, "owner");
        RegisterPlayer(other, "other");
        var giverPartyId = PlaceGiverOnMap();
        CreateIssue();
        BeginEngagement(owner, ownerPartyId, giverPartyId);
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
            instance.Call(() =>
            {
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            });

        owner.Call(() => owner.Resolve<IMessageBroker>().Publish(this, new IssueConversationOpenedLocally(issueOwnerId)));
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));
            Assert.True(giver.Issue.StartIssueWithQuest());
            Assert.IsType<Quest>(giver.Issue.IssueQuest).QuestAcceptedConsequences();
        });

    }

    private string RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));
        environment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        return partyId;
    }

    private string PlaceGiverOnMap()
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread())
                {
                    giver.PartyBelongedTo = party;
                    giver.StayingInSettlement = null;
                    party.CurrentSettlement = null;
                }
            });
        return partyId;
    }

    private void BeginEngagement(EnvironmentInstance client, string playerPartyId, string giverPartyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(giverPartyId, out var giverParty));
            Assert.True(Server.ObjectManager.TryGetId(playerParty.Party, out var playerPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(giverParty.Party, out var giverPartyBaseId));
            Assert.True(Server.Resolve<ConversationPartyTracker>().TryBeginEngagement(
                client.NetPeer, playerPartyBaseId, giverPartyBaseId, wasAiDisabled: false));
        });
    }

    [Fact]
    public void ClientCannotCreateAConquestIssue()
    {
        var client = environment.Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));

            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in potential, issueOwner));
            Assert.Null(issueOwner.Issue);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
    }
}
