using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.Ferries;
using GameInterface.Services.Ferries.Interfaces;
using GameInterface.Services.Ferries.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Rafts.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ferries;

/// <summary>
/// A player's ferry between coastal villages on the server: the fare, the fleet kept for the trip, the ferry state vanilla
/// only knows for the main party, and landing at the target.
/// </summary>
public class PlayerFerryTests : SyncTestBase
{
    private const int Gold = 1000000;

    private static readonly CampaignVec2 Port = new(new Vec2(10f, 20f), false);
    private static readonly CampaignVec2 TargetGate = new(new Vec2(60f, 70f), true);

    public PlayerFerryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void FerryLeave_PaysTheFareAsThePlayerKeepsTheFleetAndPutsThePartyOnTheFerry()
    {
        var ferry = CreatePlayerAtFerryVillage();
        var requester = Clients.First();
        requester.Call(() =>
        {
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = requester.GetRegisteredObject<MobileParty>(ferry.PartyId);
            }
            // PlayerEncounter.Finish writes the menu id, which the harness campaign doesn't have
            Campaign.Current.MapStateData ??= new MapStateData();

            // The village menu runs in a settlement encounter, which still has the village once the server's leave arrives
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterSettlementAux = requester.GetRegisteredObject<Settlement>(ferry.VillageId);
            Campaign.Current.PlayerEncounter = encounter;
        });

        requester.Call(() => requester.Resolve<IMessageBroker>().Publish(this, new EndSettlementEncounterAttempted(
            requester.GetRegisteredObject<MobileParty>(ferry.PartyId), SettlementLeaveType.Ferry)));
        Assert.Single(requester.NetworkSentMessages.GetMessages<NetworkRequestEndSettlementEncounter>());
        Server.PumpGameThread();

        Assert.Equal(SettlementEncounterLeaveOutcome.Applied,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterLeaveResult>()).Outcome);

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ferry.PartyId);

            // The fare counts the player's party, so it was priced with the player as the main party
            Assert.Equal(Gold - FerryFare(party), party.LeaderHero.Gold);
            Assert.True(party.IsInFerryState);
            Assert.True(party.IsInNavalAutoTravel);
            Assert.True(party.IsCurrentlyAtSea);
            Assert.Equal(Port.ToVec2(), party.Position.ToVec2());
            Assert.Empty(party.Ships);
            Assert.Single(CachedFleets()[ferry.HeroId].Ships);
        });

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                var party = client.GetRegisteredObject<MobileParty>(ferry.PartyId);
                Assert.True(party.IsCurrentlyAtSea);
                Assert.Empty(party.Ships);
                // Every client learns the ferry from the server, which draws it and keeps it out of reach
                Assert.True(party.IsInFerryState);
            });
        }

        // The requester boards locally, so vanilla's ferry menus steer its party
        requester.Call(() => Assert.Same(
            requester.GetRegisteredObject<Settlement>(ferry.VillageId),
            Campaign.Current.PlayerDataForNavalAutoTravel?.DeparturedVillageWithFerry));
    }

    [Fact]
    public void EndFerryRequest_LandsAtTheTargetAndGivesTheFleetBack()
    {
        var ferry = CreatePlayerAtFerryVillage();
        Server.Call(() => Server.Resolve<IPlayerFerryInterface>().StartFerry(
            Server.GetRegisteredObject<MobileParty>(ferry.PartyId),
            Server.GetRegisteredObject<Settlement>(ferry.VillageId)));
        var client = Clients.First();

        // Continue twice, as a double click would
        for (int i = 0; i < 2; i++)
        {
            client.Call(() => client.Resolve<IMessageBroker>().Publish(this, new PlayerFerryEndAttempted(client.GetRegisteredObject<MobileParty>(ferry.PartyId))));
            Server.PumpGameThread();
        }

        string shipId = null;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ferry.PartyId);
            Assert.False(party.IsInFerryState);
            Assert.False(party.IsCurrentlyAtSea);
            Assert.Equal(TargetGate.ToVec2(), party.Position.ToVec2());
            Assert.False(CachedFleets().ContainsKey(ferry.HeroId));
            Assert.True(Server.ObjectManager.TryGetId(Assert.Single(party.Ships), out shipId));
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerFerryChanged>(), message => message.VillageId == null);

        foreach (var instance in Clients)
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(ferry.PartyId);
                Assert.Same(party.Party, instance.GetRegisteredObject<Ship>(shipId).Owner);
                Assert.False(party.IsInFerryState);
                Assert.False(instance.Resolve<IPlayerFerryTracker>().Departures.ContainsKey(ferry.HeroId));
            });
        }
    }

    // A rejoin mid trip reaches the server as a character change, and the party has no ships, which vanilla puts on a raft
    [Fact]
    public void OnPlayerCharacterChanged_PartyOnFerry_KeepsTheCachedFleet()
    {
        var ferry = CreatePlayerAtFerryVillage();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ferry.PartyId);
            Server.Resolve<IPlayerFerryInterface>().StartFerry(party, Server.GetRegisteredObject<Settlement>(ferry.VillageId));

            Server.Resolve<IPlayerRaftFleetInterface>().OnPlayerCharacterChanged(party.LeaderHero, party);

            Assert.False(party.IsInRaftState);
            Assert.Empty(party.Ships);
            Assert.True(CachedFleets().ContainsKey(ferry.HeroId));
        });
    }

    // The save has no PlayerDataForNavalAutoTravel, so a client rejoining mid trip boards again from the server's departures
    [Fact]
    public void SwitchToPlayer_PartyOnFerry_BoardsTheFerry()
    {
        var ferry = CreatePlayerAtFerryVillage();
        var client = Clients.First();
        using var menuRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }));

        client.Call(() =>
        {
            var departures = new Dictionary<string, string> { [ferry.HeroId] = ferry.VillageId };
            client.Resolve<IMessageBroker>().Publish(this, new InitializeClientNavalData(new NavalPlayerData(null, null, null, null, playerFerryDepartures: departures)));

            var hero = client.GetRegisteredObject<MobileParty>(ferry.PartyId).LeaderHero;
            Assert.True(client.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(client.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            client.Resolve<IHeroInterface>().SwitchToPlayer(new Player("ferried", ferry.HeroId, ferry.PartyId, clanId, characterId));
        }, new[]
        {
            AccessTools.Method(typeof(InteractionsInitializationHandler), "Handle", new[] { typeof(MessagePayload<PlayerHeroChanged>) }),
        });
        client.PumpGameThread();

        client.Call(() => Assert.Same(
            client.GetRegisteredObject<Settlement>(ferry.VillageId),
            Campaign.Current.PlayerDataForNavalAutoTravel?.DeparturedVillageWithFerry));
        Assert.Equal(new[] { "player_ferry_state_wait" }, menuRecorder.MenusFor(client));
    }

    private record FerryTrip(string HeroId, string PartyId, string VillageId);

    // A registered player's party with gold and one ship, standing in a village whose ferry goes to another village
    private FerryTrip CreatePlayerAtFerryVillage()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var villageId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var villageComponentId = TestEnvironment.CreateRegisteredObject<Village>();
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var hullId = TestEnvironment.CreateRegisteredObject<ShipHull>();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                // Hull fields are template data every machine loads the same
                var hull = instance.GetRegisteredObject<ShipHull>(hullId);
                hull.MaxHitPoints = 100;
                hull.MaxSailHitPoints = 100;
                hull.AvailableSlots = new Dictionary<string, ShipSlot>().GetReadOnlyDictionary();

                // So are the village's ferry and positions
                var village = instance.GetRegisteredObject<Settlement>(villageId);
                var target = instance.GetRegisteredObject<Settlement>(targetId);
                using (new AllowedThread())
                {
                    village.SetSettlementComponent(instance.GetRegisteredObject<Village>(villageComponentId));
                    village.FerryTarget = target;
                    village.PortPosition = Port;
                    target.GatePosition = TargetGate;
                    instance.GetRegisteredObject<MobileParty>(partyId)._currentSettlement = village;
                }
            });
        }

        string heroId = null;
        string clanId = null;
        string characterId = null;
        Server.Call(() =>
        {
            UseFerryModel();

            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.Clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out characterId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("ferried", heroId, partyId, clanId, characterId)));

            party.LeaderHero.Gold = Gold;
            ChangeShipOwnerAction.ApplyByProduction(party.Party, new Ship(Server.GetRegisteredObject<ShipHull>(hullId)));
        });
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "ferried");

        // Clients learn every player from the player list on join
        foreach (var client in Clients)
        {
            client.Call(() => Assert.True(client.Resolve<IPlayerManager>().AddPlayer(new Player("ferried", heroId, partyId, clanId, characterId))));
        }

        return new FerryTrip(heroId, partyId, villageId);
    }

    private static int FerryFare(MobileParty party) => (int)TestFerryModel.Fare(party);

    private void UseFerryModel()
    {
        // GameModels takes the first FerryModel, which is the harness's DefaultFerryModel
        var models = Campaign.Current.Models.GetGameModels().Where(model => model is not FerryModel).ToList();
        models.Add(new TestFerryModel());
        var gameModels = new GameModels(models);
        Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
    }

    private Dictionary<string, GameInterface.Services.NavalDLC.PlayerCachedFleetData> CachedFleets() =>
        Server.Resolve<ICoopSessionProvider>().CoopSession.NavalPlayerData.PlayerCachedFleets;

    // DefaultFerryModel charges nothing. This fare counts the main party, like NavalFerryModel.
    private sealed class TestFerryModel : FerryModel
    {
        public override int MaximumFerryCapacityForPassengers => 1000;

        public override ExplainedNumber GetFerryCost(Settlement departureVillage, bool includeReason = false) =>
            new(Fare(MobileParty.MainParty), includeReason);

        public static float Fare(MobileParty party) => 500f + (party.MemberRoster.TotalManCount * 10f);
    }
}
