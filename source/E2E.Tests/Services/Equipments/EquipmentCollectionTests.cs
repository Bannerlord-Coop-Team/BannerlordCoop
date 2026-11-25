using Common.Util;
using E2E.Tests.Environment;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Equipments;

public class EquipmentCollectionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    private string ItemObjectId;
    private string EquipmentId;

    public EquipmentCollectionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerUpdateEquipmentCollection_SyncAllClients()
    {
        var server = TestEnvironment.Server;
        string? copyId = null;

        server.Call(() =>
        {
            EquipmentId = TestEnvironment.CreateRegisteredObject<Equipment>();
            ItemObjectId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var equipment));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var serverItemObject));

            // The vanilla indexer stores the struct with a typed stelem, which the transpiler has to intercept
            equipment[EquipmentIndex.Weapon0] = new EquipmentElement(serverItemObject);

            Assert.Same(serverItemObject, equipment._itemSlots[0].Item);
            Assert.Null(equipment._itemSlots[0].ItemModifier);

            // The copy constructor fills the slots of an equipment the registry already sent
            var copy = new Equipment(equipment);
            Assert.True(server.ObjectManager.TryGetId(copy, out copyId));
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(copyId, out var clientCopy));
            Assert.True(client.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var clientItemObject));
            Assert.Same(clientItemObject, clientEquipment._itemSlots[0].Item);
            Assert.Null(clientEquipment._itemSlots[0].ItemModifier);
            Assert.Same(clientItemObject, clientCopy._itemSlots[0].Item);
        }

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var equipment));

            equipment[EquipmentIndex.Weapon0] = new EquipmentElement();

            Assert.Null(equipment._itemSlots[0].Item);
            Assert.Null(equipment._itemSlots[0].ItemModifier);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.Null(clientEquipment._itemSlots[0].Item);
            Assert.Null(clientEquipment._itemSlots[0].ItemModifier);
        }
    }

    [Fact]
    public void ServerRewritesAnUnchangedSlot_ResetsAStaleClientCopy()
    {
        var server = TestEnvironment.Server;
        var client = TestEnvironment.Clients.First();

        EquipmentId = TestEnvironment.CreateRegisteredObject<Equipment>();
        ItemObjectId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var otherItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        server.Call(() => server.GetRegisteredObject<Equipment>(EquipmentId)[EquipmentIndex.Body] =
            new EquipmentElement(server.GetRegisteredObject<ItemObject>(ItemObjectId)));

        // Like an inventory screen equipping something the server then takes back
        client.Call(() =>
        {
            using (new AllowedThread())
            {
                client.GetRegisteredObject<Equipment>(EquipmentId)[EquipmentIndex.Body] =
                    new EquipmentElement(client.GetRegisteredObject<ItemObject>(otherItemId));
            }
        });

        server.Call(() => server.GetRegisteredObject<Equipment>(EquipmentId)[EquipmentIndex.Body] =
            new EquipmentElement(server.GetRegisteredObject<ItemObject>(ItemObjectId)));

        client.Call(() => Assert.Same(
            client.GetRegisteredObject<ItemObject>(ItemObjectId),
            client.GetRegisteredObject<Equipment>(EquipmentId)[EquipmentIndex.Body].Item));
    }

    [Fact]
    public void ClientUpdateEquipmentCollection_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            EquipmentId = TestEnvironment.CreateRegisteredObject<Equipment>();
            ItemObjectId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        });

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.True(firstClient.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var clientItemObject));
            clientEquipment[EquipmentIndex.Weapon0] = new EquipmentElement(clientItemObject);

            // A local copy has no network id, so its slots stay writable on the client
            var clone = clientEquipment.Clone();
            clone[EquipmentIndex.Weapon0] = new EquipmentElement(clientItemObject);
            Assert.Same(clientItemObject, clone[EquipmentIndex.Weapon0].Item);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.Null(clientEquipment._itemSlots[0].Item);
        }
    }
}
