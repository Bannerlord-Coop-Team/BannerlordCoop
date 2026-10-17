using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using IIssueOwnerResolver = GameInterface.Services.Issues.Framework.Interface.IIssueOwnerResolver;

namespace E2E.Tests.Services.Issues;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

/// <summary>
/// Gang Leader Needs Weapons is accepted through the generic accept handler, but its owners and its
/// one-quest-per-player rule come from the framework: the owner resolver sees who holds each quest,
/// the similar-commitments gate hides other players' quests from a player's own check, and the server
/// runs the framework's accept precondition check before it accepts.
/// </summary>
public sealed class GangLeaderWeaponsServerAcceptTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly GameStateManager previousGameStateManager = GameStateManager.Current;

    public GangLeaderWeaponsServerAcceptTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        environment.Dispose();
        GameStateManager._current = previousGameStateManager;
    }

    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance ClientA => environment.Clients.First();
    private EnvironmentInstance ClientB => environment.Clients.Last();

    private record Giver(string HeroId, string SettlementId);

    private record Player(string ControllerId, string HeroId, string PartyId, EnvironmentInstance Client);

    private Giver CreateGiver()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var townClanId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in environment.Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(townClanId, out var townClan));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    town.OwnerClan = townClan;
                    town.Loyalty = 50;
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.GangLeader;
                    Campaign.Current.MainParty = party;
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    var runner = new CreationCaptureRunner<Issue, GangLeaderWeaponsCreationFields>(
                        instance.Resolve<IGangLeaderWeaponsAcceptance>(), IssueBase.IssueFrequency.Common);
                    runner.ConstructAndRegisterReplicated(giver,
                        new GangLeaderWeaponsCreationFields(0, 200, CampaignTime.Days(15)));
                }
            });
        }

        var generation = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            generation = Server.Resolve<IIssueGenerationRegistry>().Bump(giver);
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                client.Resolve<IIssueGenerationRegistry>().SetGeneration(giver, generation);
            });
        }

        return new Giver(giverId, settlementId);
    }

    private Player ConnectPlayer(EnvironmentInstance client)
    {
        var controllerId = "weapons-player-" + Guid.NewGuid();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            using (new AllowedThread()) hero.Clan = clan;
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new GameInterface.Services.Players.Data.Player(controllerId, heroId, partyId, "", "")));
        });
        environment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        return new Player(controllerId, heroId, partyId, client);
    }

    private int Approach(Player player, Giver giver)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(giver.SettlementId, out var settlement));
            using (new AllowedThread()) party.CurrentSettlement = settlement;
        });

        var generation = 0;
        player.Client.Call(() =>
        {
            Assert.True(player.Client.ObjectManager.TryGetObject<Hero>(giver.HeroId, out var giverHero));
            MessageBroker.Instance.Publish(giverHero, new IssueConversationOpenedLocally(giverHero, player.ControllerId));
            Assert.True(player.Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giverHero, out generation));
        });
        return generation;
    }

    private void Request(Player player, Giver giver)
    {
        var generation = Approach(player, giver);
        player.Client.Call(() => player.Client.Resolve<INetwork>().SendAll(new RequestQuestTypeAcceptQuest(giver.HeroId, generation)));
    }

    private void RequestCompanionMission(Player player, Giver giver)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var needed = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giver.HeroId, out var giverHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            needed = giverHero.Issue.GetTotalAlternativeSolutionNeededMenCount();
            using (new AllowedThread())
            {
                hero.Gold = 100000;
                troop.Level = 20;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(troop, needed);
            }
        });

        var generation = Approach(player, giver);
        player.Client.Call(() =>
        {
            Assert.True(player.Client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(player.Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var sent = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                sent.AddToCounts(companion.CharacterObject, 1);
                sent.AddToCounts(troop, needed);
            }

            var packed = player.Client.Resolve<ITroopRosterInterface>().PackTroopRosterData(sent);
            player.Client.Resolve<INetwork>().SendAll(new RequestQuestTypeAcceptAlternative(giver.HeroId, generation, packed));
        });
    }

    private string[] Accepted() => Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>()
        .Select(message => message.OwnerId + ":" + message.OwnerControllerId).ToArray();

    private string[] AcceptedMissions() => Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>()
        .Select(message => message.OwnerId + ":" + message.OwnerControllerId).ToArray();

    private string[] Rejected() => Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>()
        .Select(message => message.OwnerId).ToArray();

    private static string OwnerSeenByFramework(EnvironmentInstance instance, Giver giver)
    {
        string owner = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giver.HeroId, out var giverHero));
            if (instance.Resolve<IIssueOwnerResolver>().TryGetOwnerControllerId(giverHero.Issue, out var controllerId))
                owner = controllerId;
        });
        return owner;
    }

    [Fact]
    public void TwoPlayersCanEachHoldOneAtTheSameTime()
    {
        var first = CreateGiver();
        var second = CreateGiver();
        var playerA = ConnectPlayer(ClientA);
        var playerB = ConnectPlayer(ClientB);

        Request(playerA, first);
        Request(playerB, second);

        Assert.Equal(new[] { first.HeroId + ":" + playerA.ControllerId, second.HeroId + ":" + playerB.ControllerId }, Accepted());
        Assert.Empty(Rejected());
        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            Assert.Equal(playerA.ControllerId, OwnerSeenByFramework(instance, first));
            Assert.Equal(playerB.ControllerId, OwnerSeenByFramework(instance, second));
        }
    }

    [Fact]
    public void ThePlayerHoldingOneIsRefusedASecond()
    {
        var first = CreateGiver();
        var second = CreateGiver();
        var playerA = ConnectPlayer(ClientA);

        Request(playerA, first);
        Request(playerA, second);

        Assert.Equal(new[] { first.HeroId + ":" + playerA.ControllerId }, Accepted());
        Assert.Equal(new[] { second.HeroId }, Rejected());
        Assert.Null(OwnerSeenByFramework(Server, second));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(second.HeroId, out var giver));
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void AnEligiblePlayerCanTakeTheCompanionMissionAndIsThenRefusedTheQuest()
    {
        var first = CreateGiver();
        var second = CreateGiver();
        var playerA = ConnectPlayer(ClientA);

        RequestCompanionMission(playerA, first);
        Request(playerA, second);

        Assert.Equal(new[] { first.HeroId + ":" + playerA.ControllerId }, AcceptedMissions());
        Assert.Empty(Accepted());
        Assert.Equal(new[] { second.HeroId }, Rejected());
        Assert.Equal(playerA.ControllerId, OwnerSeenByFramework(Server, first));
        Assert.Null(OwnerSeenByFramework(Server, second));
    }

    [Fact]
    public void AnotherPlayersQuestDoesNotCountAgainstAPlayersOwnCheck()
    {
        var first = CreateGiver();
        var second = CreateGiver();
        var playerA = ConnectPlayer(ClientA);
        var playerB = ConnectPlayer(ClientB);

        Request(playerA, first);

        Assert.True(CheckPreconditions(playerB, second));
        Assert.False(CheckPreconditions(playerA, second));
    }

    private bool CheckPreconditions(Player player, Giver giver)
    {
        var passes = false;
        player.Client.Call(() =>
        {
            Assert.True(player.Client.ObjectManager.TryGetObject<Hero>(giver.HeroId, out var giverHero));
            Assert.True(player.Client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(player.Client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            using (new MainHeroSubstitutionScope(hero, party))
                passes = giverHero.Issue.CheckPreconditions(giverHero, out _);
        });
        return passes;
    }
}
