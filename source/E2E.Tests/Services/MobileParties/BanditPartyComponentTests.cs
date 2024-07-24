using E2E.Tests.Environment;
using E2E.Tests.Util;
using E2E.Tests.Util.ObjectBuilders;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
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

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();

        // Act
        string? partyId = null;

        server.Call(() =>
        {
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

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();

        // Act
        string partyId = "TestId";
        client1.Call(() =>
        {
            BanditPartyComponent.CreateBanditParty(partyId, clan, hideout, true);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }

    [Fact]
    public void ClientUpdateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var component = GameObjectCreator.CreateInitializedObject<BanditPartyComponent>();
        var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        component.Hideout = null;
        component.IsBossParty = false;

        // Act
        client1.Call(() =>
        {
            component.IsBossParty = true;
            component.Hideout = hideout;
        });

        // Assert
        Assert.False(component.IsBossParty);
        Assert.NotEqual(component.Hideout, hideout);
    }

    [Fact]
    public void ServerUpdateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var component = GameObjectCreator.CreateInitializedObject<BanditPartyComponent>();
        HideoutBuilder hideoutBuilder = new HideoutBuilder();

        var hideout = hideoutBuilder.BuildWithSettlement();

        component.Hideout = null;
        component.IsBossParty = false;

        // Act
        server.Call(() =>
        {
            component.IsBossParty = true;
            component.Hideout = hideout;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(component.IsBossParty);
            Assert.Equal(component.Hideout, hideout);
        }
    }
}
