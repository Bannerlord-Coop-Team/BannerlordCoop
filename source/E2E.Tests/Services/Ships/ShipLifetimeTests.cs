using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

public class ShipLifetimeTests : ShipTestBase
{
    public ShipLifetimeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerCreateShip_ClientsGetHullSlotsHitPointsAndRandomValue()
    {
        var shipId = CreateServerShip();

        int serverRandomValue = 0;
        Server.Call(() => serverRandomValue = Server.GetRegisteredObject<Ship>(shipId).RandomValue);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Ship>(shipId, out var clientShip));
            Assert.Same(client.GetRegisteredObject<ShipHull>(HullId), clientShip.ShipHull);
            Assert.Equal(new[] { SlotTag }, clientShip._shipPieces.Keys);
            Assert.Null(clientShip.GetPieceAtSlot(SlotTag));
            Assert.Equal(HullMaxHitPoints, clientShip.HitPoints);
            Assert.Equal(HullMaxSailHitPoints, clientShip.SailHitPoints);
            Assert.Equal(serverRandomValue, clientShip.RandomValue);
            Assert.True(clientShip.IsTradeable);
            Assert.Equal("", clientShip.CustomSailPatternId);
            Assert.Null(clientShip.Owner);
        }
    }

    [Fact]
    public void ClientCreateShip_DoesNothing()
    {
        var client1 = Clients.First();

        client1.Call(() =>
        {
            var ship = new Ship(client1.GetRegisteredObject<ShipHull>(HullId));

            Assert.False(client1.ObjectManager.TryGetId(ship, out _));
        });
    }

    [Fact]
    public void ServerDestroyShip_RemovesShipOnClients()
    {
        var shipId = CreateServerShip();

        Server.Call(() => DestroyShipAction.Apply(Server.GetRegisteredObject<Ship>(shipId)));

        Assert.False(Server.ObjectManager.TryGetObject<Ship>(shipId, out _));
        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Ship>(shipId, out _));
        }
    }

    [Fact]
    public void ClientDestroyShip_DoesNothing()
    {
        var shipId = CreateServerShip();
        var client1 = Clients.First();

        client1.Call(() => DestroyShipAction.Apply(client1.GetRegisteredObject<Ship>(shipId)));

        Assert.True(Server.ObjectManager.TryGetObject<Ship>(shipId, out _));
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Ship>(shipId, out _));
        }
    }
}
