using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using GameInterface.Services.ObjectManager;
using Xunit.Sdk;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using static System.Net.Mime.MediaTypeNames;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Actions;

namespace E2E.Tests.Services.Equipments;

public class EquipmentDeleteTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public EquipmentDeleteTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerDeleteEquipmentOnDeath_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        Hero killHero = null;
        string? battleEquipmentId = null;
        string? civilEquipmentId = null;

        // Act
        server.Call(() =>
        {
            killHero = GameObjectCreator.CreateInitializedObject<Hero>();
            killHero._battleEquipment = new Equipment();
            killHero._civilianEquipment = new Equipment();
            Equipment battle = killHero.BattleEquipment;
            Equipment civilian = killHero.CivilianEquipment;
            Assert.True(server.ObjectManager.TryGetId(battle, out battleEquipmentId));
            Assert.True(server.ObjectManager.TryGetId(civilian, out civilEquipmentId));

            killHero.OnDeath();

        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        }

    }

    [Fact]
    public void ClientDeleteEquipmentOnDeath_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        Hero killHero = null;
        string? battleEquipmentId = null;
        string? civilEquipmentId = null;
        
        server.Call(() =>
        {
            killHero = GameObjectCreator.CreateInitializedObject<Hero>();
            killHero._battleEquipment = new Equipment();
            killHero._civilianEquipment = new Equipment();
            Assert.True(server.ObjectManager.TryGetId(killHero.BattleEquipment, out battleEquipmentId));
            Assert.True(server.ObjectManager.TryGetId(killHero.CivilianEquipment, out civilEquipmentId));
        });
        
        // Act

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
            Assert.True(client1.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));
            killHero.OnDeath();

        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
        Assert.True(server.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        }
    }
    [Fact]
    public void ServerResetEquipment_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        Hero killHero = null;
        string? battleEquipmentId = null;
        string? civilEquipmentId = null;

        // Act
        server.Call(() =>
        {
            killHero = GameObjectCreator.CreateInitializedObject<Hero>();
            killHero._battleEquipment = new Equipment();
            killHero._civilianEquipment = new Equipment();
            Equipment battle = killHero.BattleEquipment;
            Equipment civilian = killHero.CivilianEquipment;
            Assert.True(server.ObjectManager.TryGetId(battle, out battleEquipmentId));
            Assert.True(server.ObjectManager.TryGetId(civilian, out civilEquipmentId));

            killHero.ResetEquipments();

        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        }

    }

    [Fact]
    public void ClientResetEquipment_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        Hero killHero = null;
        string? battleEquipmentId = null;
        string? civilEquipmentId = null;

        server.Call(() =>
        {
            killHero = GameObjectCreator.CreateInitializedObject<Hero>();
            killHero._battleEquipment = new Equipment();
            killHero._civilianEquipment = new Equipment();
            Assert.True(server.ObjectManager.TryGetId(killHero.BattleEquipment, out battleEquipmentId));
            Assert.True(server.ObjectManager.TryGetId(killHero.CivilianEquipment, out civilEquipmentId));
        });

        // Act

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
            Assert.True(client1.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));
            killHero.ResetEquipments();

        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
        Assert.True(server.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(battleEquipmentId, out var _));
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));

        }
    }

}