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
    public void ServerChangeSettlement_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;


        // Act
        string? militiaCompId = null;

        Settlement? settlement1 = null;

        server.Call(() =>
        {
            settlement1 = GameObjectCreator.CreateInitializedObject<Settlement>();
            var settlement2 = GameObjectCreator.CreateInitializedObject<Settlement>();

            Assert.True(server.ObjectManager.TryGetId(settlement1, out string settlementId));
            Assert.True(server.ObjectManager.TryGetId(settlement2, out string settlement2Id));


            MilitiaPartyComponent militiaPartyComponent = new MilitiaPartyComponent(settlement2);
            Assert.True(server.ObjectManager.TryGetId(militiaPartyComponent, out militiaCompId));
            Assert.Equal(settlement2.StringId, militiaPartyComponent.Settlement.StringId);

            militiaPartyComponent.Settlement = settlement1;
        });


        // Assert
        Assert.True(server.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var militiaParty));
        Assert.Equal(militiaParty.Settlement, settlement1);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var clientMilitiaParty));
            Assert.Equal(clientMilitiaParty.Settlement.StringId, settlement1.StringId);
        }
    }


    [Fact]
    public void ClientChangeSettlement_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();


        Settlement? testSettlement = null; 
        Settlement? settlement = null;
        string? militiaCompId = null;

        server.Call(() =>
        {
            
            settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            testSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();

            // The settlement is not synced during ctor. Check how other public properties have been implemented to sync durinng ctor.
            MilitiaPartyComponent militiaPartyComponent = new MilitiaPartyComponent(settlement);
            militiaPartyComponent.Settlement = settlement;
            Assert.True(server.ObjectManager.TryGetId(militiaPartyComponent, out militiaCompId));

            Assert.Equal(settlement.StringId, militiaPartyComponent.Settlement.StringId);
        });

        // Act

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var serverMilitiaComponent));
            serverMilitiaComponent.Settlement = testSettlement;
        });


        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var clientMilitiaParty));
            Assert.Equal(clientMilitiaParty.Settlement.StringId, settlement.StringId);
        }
    }

}
