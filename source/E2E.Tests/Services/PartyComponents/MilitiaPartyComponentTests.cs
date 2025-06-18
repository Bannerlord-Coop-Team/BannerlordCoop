using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;


namespace E2E.Tests.Services.PartyComponents;
public class MilitiaPartyComponentTests : SyncTestBase
{
    public MilitiaPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<MilitiaPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
    }

    [Fact]
    public void Server_MilitiaPartyComponent_Properties()
    {
        TestEnvironment.AssertReferenceProperty<MilitiaPartyComponent, Settlement>(nameof(MilitiaPartyComponent.Settlement));
    }

    [Fact]
    public void ServerChangeSettlement_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;


        // Act
        string? militiaCompId = null;
        string settmentId = TestEnvironment.CreateRegisteredObject<Settlement>();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(settmentId, out var settlement));

            MilitiaPartyComponent militiaPartyComponent = new MilitiaPartyComponent(settlement);

            Assert.True(server.ObjectManager.TryGetId(militiaPartyComponent, out militiaCompId));
        });

        // Assert
        Assert.NotNull(militiaCompId);
        Assert.True(server.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var militiaParty));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var clientMilitiaParty));
            Assert.True(client.ObjectManager.TryGetId(clientMilitiaParty.Settlement, out var clientSettlementId));
            Assert.Equal(settmentId, clientSettlementId);
        }
    }

    [Fact]
    public void ClientChangeSettlement_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;


        // Act
        string? militiaCompId = null;
        string settmentId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string settment2Id = TestEnvironment.CreateRegisteredObject<Settlement>();
        var firstClient = TestEnvironment.Clients.First();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(settmentId, out var settlement));

            MilitiaPartyComponent militiaPartyComponent = new MilitiaPartyComponent(settlement);

            Assert.True(server.ObjectManager.TryGetId(militiaPartyComponent, out militiaCompId));
        });

        
        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var militiaPartyComponent));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(settment2Id, out var settlement));

            militiaPartyComponent.Settlement = settlement;
        });

        // Assert
        Assert.NotNull(militiaCompId);
        Assert.True(server.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var militiaParty));

        foreach (var client in TestEnvironment.Clients.Where(client => client != firstClient))
        {
            Assert.True(client.ObjectManager.TryGetObject<MilitiaPartyComponent>(militiaCompId, out var clientMilitiaParty));
            Assert.True(client.ObjectManager.TryGetId(clientMilitiaParty.Settlement, out var clientSettlementId));
            Assert.Equal(settmentId, clientSettlementId);
        }
    }
}