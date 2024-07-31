using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using System.IO;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;


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


        // Act
        string? partyId = null;
        string? testStringId = null;
        server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var settlementComp = GameObjectCreator.CreateInitializedObject<Town>();
            settlement.SetSettlementComponent(settlementComp);
            Assert.NotNull(settlement.SettlementComponent);
            Assert.True(server.ObjectManager.TryGetId(settlement, out testStringId));
            
            var newParty = MilitiaPartyComponent.CreateMilitiaParty("TestId", settlement);
            Assert.NotNull(newParty.HomeSettlement);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<MilitiaPartyComponent>(newParty.PartyComponent);
            Assert.NotNull(newParty.HomeSettlement);
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
        var server = TestEnvironment.Server;

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        string? newPartyId = null;
        MobileParty? newParty = null;

        server.Call(() =>
        {
            newParty = MilitiaPartyComponent.CreateMilitiaParty("TestId", settlement);
            Assert.NotNull(newParty.HomeSettlement);
            Assert.True(server.ObjectManager.TryGetId(newParty, out newPartyId));
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(newPartyId, out var clientParty));
            Assert.NotNull(clientParty);
            Assert.NotNull(clientParty.HomeSettlement);
        }

        // Act
        server.Call(() =>
        {
            newParty.RemoveParty();
            
        });

        //DestroyPartyAction.Apply(PartyBase.MainParty, newParty);

        // Assert
        Assert.NotNull(newPartyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MilitiaPartyComponent>(newPartyId, out var _));
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
