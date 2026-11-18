using Common.Messaging;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.Ferries.Interfaces;
using GameInterface.Services.Ferries.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Rafts.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
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
    private const int Gold = 5000;

    private static readonly CampaignVec2 Port = new(new Vec2(10f, 20f), false);
    private static readonly CampaignVec2 TargetGate = new(new Vec2(60f, 70f), true);

    public PlayerFerryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void StartFerry_PaysTheFareAsThePlayerKeepsTheFleetAndPutsThePartyOnTheFerry()
    {
        var ferry = CreatePlayerAtFerryVillage();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ferry.PartyId);
            var village = Server.GetRegisteredObject<Settlement>(ferry.VillageId);
            var ferryInterface = Server.Resolve<IPlayerFerryInterface>();
            Assert.True(ferryInterface.CanTakeFerry(party, village, out var reason), reason);

            ferryInterface.StartFerry(party, village);

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
            });
        }
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

        foreach (var instance in Clients)
        {
            instance.PumpGameThread();
            instance.Call(() => Assert.Same(instance.GetRegisteredObject<MobileParty>(ferry.PartyId).Party, instance.GetRegisteredObject<Ship>(shipId).Owner));
        }
    }

    [Fact]
    public void CanTakeFerry_WithoutTheFare_IsRefusedAndChargesNothing()
    {
        var ferry = CreatePlayerAtFerryVillage();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ferry.PartyId);
            party.LeaderHero.Gold = FerryFare(party) - 1;

            Assert.False(Server.Resolve<IPlayerFerryInterface>().CanTakeFerry(party, Server.GetRegisteredObject<Settlement>(ferry.VillageId), out var reason));
            Assert.Equal("you don't have enough gold to take the ferry", reason);
            Assert.False(party.IsInFerryState);
        });
    }

    // A player rejoining mid trip is at sea with no ships, which the raft code would otherwise put on a raft
    [Fact]
    public void RejoinOnFerry_KeepsTheFleetForLandingAndDoesntRaft()
    {
        var ferry = CreatePlayerAtFerryVillage();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ferry.PartyId);
            Server.Resolve<IPlayerFerryInterface>().StartFerry(party, Server.GetRegisteredObject<Settlement>(ferry.VillageId));

            Server.Resolve<IPlayerRaftFleetInterface>().OnPlayerCharacterChanged(party.LeaderHero, party);

            Assert.False(party.IsInRaftState);
            Assert.True(CachedFleets().ContainsKey(ferry.HeroId));
            Assert.Empty(party.Ships);
        });
    }

    private record FerryTrip(string HeroId, string PartyId, string VillageId);

    // A registered player's party with gold and one ship, standing in a village whose ferry goes to another village
    private FerryTrip CreatePlayerAtFerryVillage()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var villageId = TestEnvironment.CreateRegisteredObject<Settlement>();
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
            });
        }

        string heroId = null;
        Server.Call(() =>
        {
            UseFerryModel();

            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("ferried", heroId, partyId, clanId, characterId)));

            party.LeaderHero.Gold = Gold;
            ChangeShipOwnerAction.ApplyByProduction(party.Party, new Ship(Server.GetRegisteredObject<ShipHull>(hullId)));

            var village = Server.GetRegisteredObject<Settlement>(villageId);
            var target = Server.GetRegisteredObject<Settlement>(targetId);
            village.FerryTarget = target;
            village.PortPosition = Port;
            target.GatePosition = TargetGate;
            party._currentSettlement = village;
        });
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "ferried");

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

    // DefaultFerryModel charges nothing and carries no one. This fare counts the main party, like NavalFerryModel.
    private sealed class TestFerryModel : FerryModel
    {
        public override int MaximumFerryCapacityForPassengers => 50;

        public override ExplainedNumber GetFerryCost(Settlement departureVillage, bool includeReason = false) =>
            new(Fare(MobileParty.MainParty), includeReason);

        public static float Fare(MobileParty party) => 500f + (party.MemberRoster.TotalManCount * 10f);
    }
}
