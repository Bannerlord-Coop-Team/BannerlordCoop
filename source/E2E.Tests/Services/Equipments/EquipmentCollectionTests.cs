using E2E.Tests.Environment;
using GameInterface.Services.Equipments.Messages;
using GameInterface.Services.Equipments.Patches;
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

        server.Call(() =>
        {
            EquipmentId = TestEnvironment.CreateRegisteredObject<Equipment>();
            ItemObjectId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var equipment));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var serverItemObject));
            var element = new EquipmentElement(serverItemObject);

            EquipmentCollectionPatches.ArrayAssignIntercept<EquipmentElement, ItemSlotsArrayUpdated>(
                equipment._itemSlots,
                0,
                element,
                equipment);

            Assert.Same(serverItemObject, equipment._itemSlots[0].Item);
            Assert.Null(equipment._itemSlots[0].ItemModifier);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.True(client.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var clientItemObject));
            Assert.Same(clientItemObject, clientEquipment._itemSlots[0].Item);
            Assert.Null(clientEquipment._itemSlots[0].ItemModifier);
        }

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var equipment));

            EquipmentCollectionPatches.ArrayAssignIntercept<EquipmentElement, ItemSlotsArrayUpdated>(
                equipment._itemSlots,
                0,
                new EquipmentElement(),
                equipment);

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
    public void ClientUpdateEquipmentCollection_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        EquipmentElement element = new EquipmentElement();

        server.Call(() =>
        {
            EquipmentId = TestEnvironment.CreateRegisteredObject<Equipment>();
            ItemObjectId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var serverEquipment));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var serverItemObject));
            element = new EquipmentElement(serverItemObject);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var syncedEquipment));
        }

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var clientItemObject));
            EquipmentCollectionPatches.ArrayAssignIntercept<EquipmentElement, ItemSlotsArrayUpdated>(clientEquipment._itemSlots, 0, element, clientEquipment);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.NotEqual(element.Item, clientEquipment._itemSlots[0].Item);
        }
    }
}
