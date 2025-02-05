using E2E.Tests.Environment;
using Xunit.Abstractions;
using TaleWorlds.Core;
using HarmonyLib;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Equipments.Patches;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using System.Xml.Linq;
using TaleWorlds.ObjectSystem;

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
        // Arrange
        var server = TestEnvironment.Server;
        EquipmentElement element = new EquipmentElement();
        // Act

        server.Call(() =>
        {
            EquipmentId = TestEnvironment.CreateRegisteredObject<Equipment>();
            ItemObjectId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var Equipment));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(ItemObjectId, out var serverItemObject));
            EquipmentElement element = new EquipmentElement(serverItemObject);
            EquipmentCollectionPatches.ArrayAssignIntercept(Equipment._itemSlots, 0, element, Equipment);
            Assert.Equal(element.Item, Equipment._itemSlots[0].Item);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var Equip));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.Equal(element.Item, clientEquipment._itemSlots[0].Item);
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
            EquipmentCollectionPatches.ArrayAssignIntercept(clientEquipment._itemSlots, 0, element, clientEquipment);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var clientEquipment));
            Assert.NotEqual(element.Item, clientEquipment._itemSlots[0].Item);
        }
    }
}