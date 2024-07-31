using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.ObjectManager;
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


            serverComponent.InitializeQuestPartyProperties(
                mobileParty,
                new Vec2(0, 0), 
                5f,
                settlement,
                new TextObject("ServerName"),
                GameObjectCreator.CreateInitializedObject<PartyTemplateObject>(),
                hero,
                5,
                "mount",
                "harness",
                5,
                false);
        });

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var clientComponent));
            Assert.True(client1.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var serverComponent));
            Assert.True(client1.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(client1.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            serverComponent.InitializeQuestPartyProperties(
                mobileParty,
                new Vec2(0, 0),
                5f,
                settlement,
                new TextObject("ClientName"),
                GameObjectCreator.CreateInitializedObject<PartyTemplateObject>(),
                hero,
                5,
                "mount",
                "harness",
                5,
                false);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var serverComponent));
        Assert.Equal(new TextObject("ServerName"), serverComponent._name);
        Assert.NotNull(serverComponent._homeSettlement);
        Assert.NotNull(serverComponent._partyHarnessStringId);
        Assert.NotNull(serverComponent._partyMountStringId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var clientComponent));
            Assert.Equal(serverComponent._name, clientComponent._name);
            Assert.NotNull(clientComponent._homeSettlement);
            Assert.Equal(5f, clientComponent._customPartyBaseSpeed);
            Assert.NotNull(clientComponent._partyHarnessStringId);
            Assert.NotNull(clientComponent._partyMountStringId);
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

            serverComponent._name = new TextObject("TestComponent");
            serverComponent._homeSettlement = settlement;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<CustomPartyComponent>(componentId, out var clientComponent));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutId, out var settlement));

            Assert.True(clientComponent._name.Value.Equals("TestComponent"));
            Assert.Equal(clientComponent._homeSettlement, settlement);
        }
    }
}
