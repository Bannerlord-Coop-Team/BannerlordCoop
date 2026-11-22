#if COOP_NAVAL
using Autofac;
using Common.Messaging;
using Common.Util;
using Coop.Naval;
using Coop.Naval.PortScreen.Handlers;
using Coop.Naval.PortScreen.Interfaces;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>
/// Mooring ships at a port: the server keeps every clan's moored ships, and only the owner's client lists its ship.
/// </summary>
public class PortMooringSyncTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private readonly string settlementId;

    private EnvironmentInstance Server => testEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => testEnvironment.Clients;

    // Mooring lives in Coop.Naval, which the shared environment doesn't register
    public PortMooringSyncTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output, configureDependencies: builder => builder.RegisterModule<CoopNavalModule>());
        settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
    }

    public void Dispose()
    {
        testEnvironment.Dispose();
    }

    [Fact]
    public void MoorAndTakeBack_OnlyTheOwnerClientListsTheShipAndOnlyTheOwnerClanTakesItBack()
    {
        var clients = Clients.ToArray();
        var preCoopShipId = StagePort();
        var first = StagePlayerWithShip("first_player", clients[0]);
        var second = StagePlayerWithShip("second_player", clients[1]);

        Server.Call(() =>
        {
            Assert.True(Moor(first));
            Assert.True(Moor(second));
        });
        PumpClients();

        // Stashing a ship clears its owner on every machine, even where the stash doesn't list it
        foreach (var client in clients)
        {
            client.Call(() =>
            {
                Assert.Null(client.GetRegisteredObject<Ship>(first.ShipId).Owner);
                Assert.Null(client.GetRegisteredObject<Ship>(second.ShipId).Owner);
            });
        }
        AssertStash(clients[0], first.ShipId);
        AssertStash(clients[1], second.ShipId);

        Server.Call(() =>
        {
            Assert.False(Retrieve(first, preCoopShipId));
            Assert.False(Retrieve(second, first.ShipId));
            Assert.True(Retrieve(first, first.ShipId));
        });
        PumpClients();

        foreach (var client in clients)
        {
            client.Call(() =>
            {
                var ship = client.GetRegisteredObject<Ship>(first.ShipId);
                var party = client.GetRegisteredObject<MobileParty>(first.PartyId).Party;
                Assert.Same(party, ship.Owner);
                Assert.Contains(ship, party.Ships);
            });
        }
        AssertStash(clients[0]);
        AssertStash(clients[1], second.ShipId);
        AssertStash(Server, preCoopShipId, second.ShipId);

        // Taking back the first ship moved the second one down a slot, so its owner has to move with it
        Server.Call(() => Assert.True(Retrieve(second, second.ShipId)));
    }

    // The joined save's stashes hold every clan's moored ships, so the first hero change after joining keeps only the player clan's
    [Fact]
    public void JoinFilter_KeepsOnlyTheClanShipsOnce()
    {
        var client = Clients.First();
        var unrecordedSettlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var clanId = testEnvironment.CreateRegisteredObject<Clan>();
        var ownShipId = testEnvironment.CreateRegisteredObject<Ship>();
        var otherShipId = testEnvironment.CreateRegisteredObject<Ship>();
        var unrecordedShipId = testEnvironment.CreateRegisteredObject<Ship>();

        client.Call(() =>
        {
            var hero = client.GetRegisteredObject<Hero>(heroId);
            var clan = client.GetRegisteredObject<Clan>(clanId);
            using (new AllowedThread())
            {
                clan.InitMembers();
                hero.Clan = clan;
            }

            var port = client.GetRegisteredObject<Settlement>(settlementId);
            var unrecorded = client.GetRegisteredObject<Settlement>(unrecordedSettlementId);
            var ownShip = client.GetRegisteredObject<Ship>(ownShipId);
            var otherShip = client.GetRegisteredObject<Ship>(otherShipId);
            port.ShipStash = new MBList<Ship> { ownShip, otherShip };
            unrecorded.ShipStash = new MBList<Ship> { client.GetRegisteredObject<Ship>(unrecordedShipId) };
            Campaign.Current.CampaignObjectManager.Settlements = new MBList<Settlement> { port, unrecorded };

            var ownerClans = new Dictionary<string, List<string>> { [settlementId] = new List<string> { clanId, "other_clan" } };
            var navalPlayerData = new NavalPlayerData(null, null, null, null, ownerClans);
            // Other naval PlayerHeroChanged subscribers need a full campaign, so the hero changes go straight to the handler
            var handler = client.Resolve<PortMooringHandler>();
            client.Resolve<IMessageBroker>().Publish(this, new InitializeClientMooredShips(navalPlayerData));
            handler.Handle_PlayerHeroChanged(new MessagePayload<PlayerHeroChanged>(this, new PlayerHeroChanged(null, hero)));

            Assert.Equal(new[] { ownShip }, port.ShipStash);
            Assert.Empty(unrecorded.ShipStash);

            // A session update republishes the naval data, and the hero change that follows must not filter again
            port.ShipStash.Add(otherShip);
            client.Resolve<IMessageBroker>().Publish(this, new InitializeClientNavalData(navalPlayerData));
            handler.Handle_PlayerHeroChanged(new MessagePayload<PlayerHeroChanged>(this, new PlayerHeroChanged(hero, hero)));

            Assert.Equal(new[] { ownShip, otherShip }, port.ShipStash);
        });
    }

    // Returns a ship moored before co-op, which only the server's stash holds and no player owns
    private string StagePort()
    {
        var preCoopShipId = testEnvironment.CreateRegisteredObject<Ship>();

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                settlement.HasPort = true;
                settlement.ShipStash ??= new MBList<Ship>();
            });
        }
        Server.Call(() => Server.GetRegisteredObject<Settlement>(settlementId).ShipStash.Add(Server.GetRegisteredObject<Ship>(preCoopShipId)));

        return preCoopShipId;
    }

    // A player in their own clan, inside the port with one ship
    private StagedPlayer StagePlayerWithShip(string controllerId, EnvironmentInstance client)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        var clanId = testEnvironment.CreateRegisteredObject<Clan>();
        var shipId = testEnvironment.CreateRegisteredObject<Ship>();

        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(heroId);
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var clan = Server.GetRegisteredObject<Clan>(clanId);
            using (new AllowedThread())
            {
                clan.InitMembers();
                hero.Clan = clan;
                party._currentSettlement = Server.GetRegisteredObject<Settlement>(settlementId);
            }
            Assert.False(string.IsNullOrEmpty(clan.StringId));

            ChangeShipOwnerAction.ApplyByProduction(party.Party, Server.GetRegisteredObject<Ship>(shipId));

            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.AddPlayer(new Player(controllerId, heroId, partyId, clanId, null)));
            players.SetPeer(controllerId, client.NetPeer);
        });
        PumpClients();

        return new StagedPlayer(heroId, partyId, shipId);
    }

    // Call inside Server.Call
    private bool Moor(StagedPlayer player) => Server.Resolve<IPortMooringInterface>().TryMoorShip(
        Server.GetRegisteredObject<Hero>(player.HeroId),
        Server.GetRegisteredObject<MobileParty>(player.PartyId),
        Server.GetRegisteredObject<Settlement>(settlementId),
        Server.GetRegisteredObject<Ship>(player.ShipId));

    // Call inside Server.Call
    private bool Retrieve(StagedPlayer player, string shipId) => Server.Resolve<IPortMooringInterface>().TryRetrieveShip(
        Server.GetRegisteredObject<Hero>(player.HeroId),
        Server.GetRegisteredObject<MobileParty>(player.PartyId),
        Server.GetRegisteredObject<Settlement>(settlementId),
        Server.GetRegisteredObject<Ship>(shipId));

    private void AssertStash(EnvironmentInstance instance, params string[] shipIds)
    {
        instance.Call(() =>
        {
            var expected = shipIds.Select(shipId => instance.GetRegisteredObject<Ship>(shipId));
            Assert.Equal(expected, instance.GetRegisteredObject<Settlement>(settlementId).ShipStash);
        });
    }

    private void PumpClients()
    {
        foreach (var client in Clients)
        {
            client.PumpGameThread();
        }
    }

    private sealed record StagedPlayer(string HeroId, string PartyId, string ShipId);
}
#endif
