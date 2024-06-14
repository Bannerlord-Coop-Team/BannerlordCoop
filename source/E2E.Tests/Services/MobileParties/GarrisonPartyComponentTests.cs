using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class GarrisonPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironement { get; }
    public GarrisonPartyComponentTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.Town = GameObjectCreator.CreateInitializedObject<Town>();

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var newParty = GarrisonPartyComponent.CreateGarrisonParty("TestId", settlement, true);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<GarrisonPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.Town = GameObjectCreator.CreateInitializedObject<Town>();

        // Act
        string partyId = "TestId";
        client1.Call(() =>
        {
            GarrisonPartyComponent.CreateGarrisonParty("TestId", settlement, true);
        });

        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}
