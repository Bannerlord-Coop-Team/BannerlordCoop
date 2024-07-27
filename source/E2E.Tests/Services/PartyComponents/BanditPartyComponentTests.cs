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

        string componentId = TestEnvironment.CreateRegisteredObject<BanditPartyComponent>();
        Assert.True(server.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var serverComponent));

        HideoutBuilder hideoutBuilder = new HideoutBuilder();
        var hideout = hideoutBuilder.BuildWithSettlement();

        serverComponent.Hideout = hideout;

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var clientComponent));
            clientComponent.IsBossParty = true;
            clientComponent.Hideout = null;
        });

        // Assert
        Assert.False(serverComponent.IsBossParty);
        Assert.NotNull(serverComponent.Hideout);
    }

    [Fact]
    public void ServerUpdateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        string componentId = TestEnvironment.CreateRegisteredObject<BanditPartyComponent>();
        
        HideoutBuilder hideoutBuilder = new HideoutBuilder();
        var hideout = hideoutBuilder.BuildWithSettlement();

        // Act
        server.Call(() =>
        {
            server.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var serverComponent);
            serverComponent.IsBossParty = true;
            serverComponent.Hideout = hideout;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<BanditPartyComponent>(componentId, out var clientComponent));
            Assert.True(clientComponent.IsBossParty);
            Assert.Equal(clientComponent.Hideout, hideout);
        }
    }
}
