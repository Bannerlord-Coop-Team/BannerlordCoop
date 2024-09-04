using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using E2E.Tests.Util.ObjectBuilders;
using GameInterface.Services.ObjectManager;
using System.ComponentModel;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class BanditPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public BanditPartyComponentTests(ITestOutputHelper output)
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
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var newParty = BanditPartyComponent.CreateBanditParty("TestId", clan, hideout, true);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<BanditPartyComponent>(newParty.PartyComponent);
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
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var isBossParty = false;
            partyComponent = new BanditPartyComponent(hideout, isBossParty);
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
        var componentId = TestEnvironment.CreateRegisteredObject<BanditPartyComponent>();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var serverComponent));
            Assert.True(server.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));

            serverComponent.IsBossParty = false;
            serverComponent.Hideout = hideout;
        });

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var clientComponent));
            clientComponent.IsBossParty = true;
            clientComponent.Hideout = null;
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var serverComponent));
        Assert.False(serverComponent.IsBossParty);
        Assert.NotNull(serverComponent.Hideout);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var clientComponent));
            Assert.False(clientComponent.IsBossParty);
            Assert.NotNull(clientComponent.Hideout);
        }
    }

    [Fact]
    public void ServerUpdateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Create objects on the server and all clients, this returns the "network id" of the object
        var componentId = TestEnvironment.CreateRegisteredObject<BanditPartyComponent>();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var serverComponent));
            Assert.True(server.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));

            serverComponent.IsBossParty = true;
            serverComponent.Hideout = hideout;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var clientComponent));
            Assert.True(client.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));
            Assert.True(clientComponent.IsBossParty);
            Assert.Equal(clientComponent.Hideout, hideout);
        }
    }
}
