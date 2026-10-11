using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

public class ShipSyncTests : ShipTestBase
{
    public ShipSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerOwnerChange_MovesShipBetweenPartyShipListsOnClients()
    {
        var firstPartyBaseId = CreatePartyBase();
        var secondPartyBaseId = CreatePartyBase();
        var shipId = CreateServerShip();

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

        Server.Call(() => ChangeShipOwnerAction.ApplyByTransferring(
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
    public void ServerDestroyOwnedShip_RemovesItFromThePartyAndRegistryOnClients()
    {
        var partyBaseId = CreatePartyBase();
        var shipId = CreateServerShip();
        Server.Call(() => ChangeShipOwnerAction.ApplyByProduction(
            Server.GetRegisteredObject<PartyBase>(partyBaseId),
            Server.GetRegisteredObject<Ship>(shipId)));
        var clientShips = Clients.ToDictionary(client => client, client => client.GetRegisteredObject<Ship>(shipId));

        Server.Call(() => DestroyShipAction.Apply(Server.GetRegisteredObject<Ship>(shipId)));

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Ship>(shipId, out _));
            Assert.Null(clientShips[client].Owner);
            Assert.DoesNotContain(clientShips[client], client.GetRegisteredObject<PartyBase>(partyBaseId).Ships);
        }
    }

    [Fact]
    public void ServerHitPointChanges_ReachClientsOnTheCoalescerFlush()
    {
        var shipId = CreateServerShip();

        Server.Call(() =>
        {
            var ship = Server.GetRegisteredObject<Ship>(shipId);
            ship.HitPoints = 700f;
            ship.HitPoints = 400f;
            ship.SailHitPoints = 100f;
        });

        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            var ship = client.GetRegisteredObject<Ship>(shipId);
            Assert.Equal(400f, ship.HitPoints);
            Assert.Equal(100f, ship.SailHitPoints);
        }
    }

    [Fact]
    public void ServerSetName_SyncsToClients()
    {
        var shipId = CreateServerShip();

        Server.Call(() => Server.GetRegisteredObject<Ship>(shipId).SetName(new TextObject("Sea Wolf")));

        foreach (var client in Clients)
        {
            Assert.Equal("Sea Wolf", client.GetRegisteredObject<Ship>(shipId)._name.Value);
        }
    }

    [Fact]
    public void ServerFlagChanges_SyncToClients()
    {
        var shipId = CreateServerShip();

        Server.Call(() =>
        {
            var ship = Server.GetRegisteredObject<Ship>(shipId);
            ship.IsInvulnerable = true;
            ship.IsTradeable = false;
            ship.IsUsedByQuest = true;
            ship.CustomSailPatternId = "coop_test_sail_pattern";
        });

        foreach (var client in Clients)
        {
            var ship = client.GetRegisteredObject<Ship>(shipId);
            Assert.True(ship.IsInvulnerable);
            Assert.False(ship.IsTradeable);
            Assert.True(ship.IsUsedByQuest);
            Assert.Equal("coop_test_sail_pattern", ship.CustomSailPatternId);
        }
    }

    [Fact]
    public void ClientOwnerChange_StaysLocal()
    {
        var partyBaseId = CreatePartyBase();
        var shipId = CreateServerShip();
        var client1 = Clients.First();

        client1.Call(() => ChangeShipOwnerAction.ApplyByTransferring(
            client1.GetRegisteredObject<PartyBase>(partyBaseId),
            client1.GetRegisteredObject<Ship>(shipId)));

        Assert.Null(Server.GetRegisteredObject<Ship>(shipId).Owner);
        foreach (var client in Clients.Skip(1))
        {
            Assert.Null(client.GetRegisteredObject<Ship>(shipId).Owner);
        }
    }

    private string CreatePartyBase()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        string? partyBaseId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(
            Server.GetRegisteredObject<MobileParty>(partyId).Party, out partyBaseId)));

        Assert.NotNull(partyBaseId);
        return partyBaseId!;
    }
}
