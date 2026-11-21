using Common.Util;
using Coop.Naval.PortScreen.Interfaces;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
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
public class PortMooringSyncTests : SyncTestBase
{
    private readonly string settlementId;

    public PortMooringSyncTests(ITestOutputHelper output) : base(output)
    {
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
    }

    [Fact]
    public void MoorAndTakeBack_OnlyTheOwnerClientListsTheShipAndOnlyTheOwnerClanTakesItBack()
    {
        var clients = Clients.ToArray();
        StagePort();
        var first = StagePlayerWithShip("first_player", clients[0]);
        var second = StagePlayerWithShip("second_player", clients[1]);

        Server.Call(() =>
        {
            Assert.True(Moor(first));
            Assert.True(Moor(second));
        });
        PumpClients();

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
        AssertStash(Server, second.ShipId);
    }

    private void StagePort()
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                settlement.HasPort = true;
                settlement.ShipStash ??= new MBList<Ship>();
            });
        }
    }

    // A player in their own clan, inside the port with one ship
    private StagedPlayer StagePlayerWithShip(string controllerId, EnvironmentInstance client)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var shipId = TestEnvironment.CreateRegisteredObject<Ship>();

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
