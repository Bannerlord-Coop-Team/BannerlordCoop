using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>
/// Party ship lists on clients after the owner changes and destroys that naval battle results apply on the server.
/// </summary>
public class ShipOwnershipSyncTests : SyncTestBase
{
    public ShipOwnershipSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerOwnerChange_MovesShipBetweenPartyShipListsOnClients()
    {
        var shipId = TestEnvironment.CreateRegisteredObject<Ship>();
        var firstPartyBaseId = TestEnvironment.CreateRegisteredObject<PartyBase>();
        var secondPartyBaseId = TestEnvironment.CreateRegisteredObject<PartyBase>();

        Server.Call(() => ChangeShipOwnerAction.ApplyByProduction(
            Server.GetRegisteredObject<PartyBase>(firstPartyBaseId),
            Server.GetRegisteredObject<Ship>(shipId)));

        foreach (var client in Clients)
        {
            var ship = client.GetRegisteredObject<Ship>(shipId);
            var firstOwner = client.GetRegisteredObject<PartyBase>(firstPartyBaseId);
            Assert.Same(firstOwner, ship.Owner);
            Assert.Contains(ship, firstOwner.Ships);
        }

        Server.Call(() => ChangeShipOwnerAction.ApplyByLooting(
            Server.GetRegisteredObject<PartyBase>(secondPartyBaseId),
            Server.GetRegisteredObject<Ship>(shipId)));

        foreach (var client in Clients)
        {
            var ship = client.GetRegisteredObject<Ship>(shipId);
            var firstOwner = client.GetRegisteredObject<PartyBase>(firstPartyBaseId);
            var secondOwner = client.GetRegisteredObject<PartyBase>(secondPartyBaseId);
            Assert.Same(secondOwner, ship.Owner);
            Assert.DoesNotContain(ship, firstOwner.Ships);
            Assert.Contains(ship, secondOwner.Ships);
        }
    }

    [Fact]
    public void ServerDestroyOwnedShip_RemovesItFromThePartyShipListOnClients()
    {
        var shipId = TestEnvironment.CreateRegisteredObject<Ship>();
        var partyBaseId = TestEnvironment.CreateRegisteredObject<PartyBase>();

        Server.Call(() => ChangeShipOwnerAction.ApplyByProduction(
            Server.GetRegisteredObject<PartyBase>(partyBaseId),
            Server.GetRegisteredObject<Ship>(shipId)));
        var clientShips = Clients.ToDictionary(client => client, client => client.GetRegisteredObject<Ship>(shipId));

        Server.Call(() => DestroyShipAction.Apply(Server.GetRegisteredObject<Ship>(shipId)));

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            Assert.False(client.ObjectManager.TryGetObject<Ship>(shipId, out _));
            Assert.Null(clientShips[client].Owner);
            Assert.DoesNotContain(clientShips[client], client.GetRegisteredObject<PartyBase>(partyBaseId).Ships);
        }
    }
}
