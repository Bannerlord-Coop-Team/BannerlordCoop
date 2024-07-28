using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using System.ComponentModel;
using Coop.IntegrationTests.Environment;

namespace E2E.Tests.Services.PartyComponents;
public class MilitiaPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public MilitiaPartyComponentTests(ITestOutputHelper output)
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

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var newParty = MilitiaPartyComponent.CreateMilitiaParty("TestId", settlement);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<MilitiaPartyComponent>(newParty.PartyComponent);
        }
    }


    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            partyComponent = new MilitiaPartyComponent(settlement);
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }


    [Fact]
    public void ServerUpdateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var newParty = MilitiaPartyComponent.CreateMilitiaParty("TestId", settlement);

        // Act

        server.Call(() =>
        {
            newParty.RemoveParty();
        });


        // Assert
        Assert.Null(newParty.HomeSettlement);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MilitiaPartyComponent>("TestId", out var component));
            Assert.Null(component.Settlement);
        }
    }

    // TBD
    /*
    [Fact]
    public void ClientUpdateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        
        // Act
        string partyId = "TestId";
        client1.Call(() =>
        {
            
        });

        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
    */
}
