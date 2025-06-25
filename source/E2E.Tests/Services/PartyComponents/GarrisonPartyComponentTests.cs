using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class GarrisonPartyComponentTests : SyncTestBase
{
    string ComponentId;
    public GarrisonPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        ComponentId = TestEnvironment.CreateRegisteredObject<GarrisonPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Settlement>();

    }

    [Fact]
    public void Server_GarrisonPartyComponent_Properties()
    {
        Server.ObjectManager.TryGetObject(ComponentId, out GarrisonPartyComponent component);
        component.Settlement = null;
        TestEnvironment.AssertReferenceProperty<GarrisonPartyComponent, Settlement>(nameof(GarrisonPartyComponent.Settlement));
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;
        string? settlementId = null;

        server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement.Town = GameObjectCreator.CreateInitializedObject<Town>();

            var newSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();

            var newParty = GarrisonPartyComponent.CreateGarrisonParty("TestId", settlement, true);
            GarrisonPartyComponent garrison = (GarrisonPartyComponent)newParty.PartyComponent;
            garrison.Settlement = newSettlement;

            Assert.True(server.ObjectManager.TryGetId(newSettlement, out settlementId));

            Assert.True(server.ObjectManager.TryGetId(newParty, out partyId));
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<GarrisonPartyComponent>(newParty.PartyComponent);
            GarrisonPartyComponent garrison = (GarrisonPartyComponent)newParty.PartyComponent;
            Assert.True(client.ObjectManager.TryGetId(garrison.Settlement, out string clientGarrisonSettlementId));

            Assert.Equal(settlementId, clientGarrisonSettlementId);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.Town = GameObjectCreator.CreateInitializedObject<Town>();

        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            partyComponent = new GarrisonPartyComponent(settlement);
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
