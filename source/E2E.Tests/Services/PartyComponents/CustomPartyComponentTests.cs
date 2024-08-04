using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class CustomPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public CustomPartyComponentTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var spawnSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            var name = new TextObject("Name");
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var partyTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();

            var newParty = CustomPartyComponent.CreateQuestParty(new Vec2(5, 5), 5, spawnSettlement, name, clan, partyTemplate, hero);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<CustomPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();


        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            partyComponent = new CustomPartyComponent();
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }

    [Fact]
    public void ClientUpdateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Create objects on the server and all clients, this returns the "network id" of the object
        var componentId = TestEnvironment.CreateRegisteredObject<CustomPartyComponent>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var serverComponent));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            CustomPartyComponentPatches.NameIntercept(serverComponent, new TextObject("ServerName"));
            CustomPartyComponentPatches.HomeSettlementIntercept(serverComponent, settlement);
            CustomPartyComponentPatches.BaseSpeedIntercept(serverComponent, 5f);
            CustomPartyComponentPatches.HarnessIdIntercept(serverComponent, "harness");
            CustomPartyComponentPatches.MountIdIntercept(serverComponent, "mount");
            CustomPartyComponentPatches.AvoidHostileActionsIntercept(serverComponent, true);
        });

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var clientComponent));
            Assert.True(client1.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(client1.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            CustomPartyComponentPatches.NameIntercept(clientComponent, new TextObject("ClientName"));
            CustomPartyComponentPatches.HomeSettlementIntercept(clientComponent, null);
            CustomPartyComponentPatches.BaseSpeedIntercept(clientComponent, 55f);
            CustomPartyComponentPatches.HarnessIdIntercept(clientComponent, null);
            CustomPartyComponentPatches.MountIdIntercept(clientComponent, null);
            CustomPartyComponentPatches.AvoidHostileActionsIntercept(clientComponent, false);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var serverComponent));
        Assert.Equal(new TextObject("ServerName").Value, serverComponent._name.Value);
        Assert.NotNull(serverComponent._homeSettlement);
        Assert.Equal(5f, serverComponent._customPartyBaseSpeed);
        Assert.NotNull(serverComponent._partyHarnessStringId);
        Assert.NotNull(serverComponent._partyMountStringId);
        Assert.True(serverComponent._avoidHostileActions);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var clientComponent));
            Assert.Equal(serverComponent._name.Value, clientComponent._name.Value);
            Assert.NotNull(clientComponent._homeSettlement);
            Assert.Equal(5f, clientComponent._customPartyBaseSpeed);
            Assert.NotNull(clientComponent._partyHarnessStringId);
            Assert.NotNull(clientComponent._partyMountStringId);
            Assert.True(clientComponent._avoidHostileActions);
        }
    }

    [Fact]
    public void ServerUpdateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Create objects on the server and all clients, this returns the "network id" of the object
        var componentId = TestEnvironment.CreateRegisteredObject<CustomPartyComponent>();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Settlement>();


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var serverComponent));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var settlement));

            CustomPartyComponentPatches.NameIntercept(serverComponent, new TextObject("TestComponent"));
            CustomPartyComponentPatches.HomeSettlementIntercept(serverComponent, settlement);
            CustomPartyComponentPatches.BaseSpeedIntercept(serverComponent, 5f);
            CustomPartyComponentPatches.HarnessIdIntercept(serverComponent, "harness");
            CustomPartyComponentPatches.MountIdIntercept(serverComponent, "mount");
            CustomPartyComponentPatches.AvoidHostileActionsIntercept(serverComponent, true);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var clientComponent));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutId, out var settlement));

            Assert.True(clientComponent._name.Value.Equals("TestComponent"));
            Assert.Equal(clientComponent._homeSettlement, settlement);
            Assert.Equal(5f, clientComponent._customPartyBaseSpeed);
            Assert.Equal("harness", clientComponent._partyHarnessStringId);
            Assert.Equal("mount", clientComponent._partyMountStringId);
            Assert.True(clientComponent._avoidHostileActions);
        }
    }
}
