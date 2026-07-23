using Common.Messaging;
using E2E.Tests.Environment;
using GameInterface.Services.Locations.Messages;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// Verifies that mutations of <see cref="Location.SpecialItems"/> are server authoritative and
/// synced to every client
/// </summary>
public class LocationSpecialItemsSyncTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public LocationSpecialItemsSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerAddSpecialItem_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            location.AddSpecialItem(item);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Single(serverLocation.SpecialItems);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));

            var clientItem = Assert.Single(location.SpecialItems);
            Assert.True(client.ObjectManager.TryGetId(clientItem, out var clientItemId));
            Assert.Equal(itemId, clientItemId);
        }
    }

    [Fact]
    public void ServerRemoveSpecialItem_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            location.AddSpecialItem(item);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Single(location.SpecialItems);
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            // Vanilla only removes special items from inside a live mission scene, so the server
            // applies the list removal directly and publishes the internal event that broadcasts
            // the removal to clients.
            location.SpecialItems.Remove(item);
            MessageBroker.Instance.Publish(location, new LocationSpecialItemRemoved(location, item));
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Empty(serverLocation.SpecialItems);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Empty(location.SpecialItems);
        }
    }

    [Fact]
    public void ClientAddSpecialItem_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.True(client1.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            location.AddSpecialItem(item);

            // Client additions are blocked; the synced collection keeps the server's state.
            Assert.Empty(location.SpecialItems);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Empty(serverLocation.SpecialItems);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Empty(location.SpecialItems);
        }
    }
}
