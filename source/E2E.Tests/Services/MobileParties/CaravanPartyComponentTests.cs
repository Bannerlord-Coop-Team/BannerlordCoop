using Autofac.Features.OwnedInstances;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class CaravanPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public CaravanPartyComponentTests(ITestOutputHelper output)
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

        var owner = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();

        settlement.Culture = culture;

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var newParty = CaravanPartyComponent.CreateCaravanParty(owner, settlement, caravanLeader: owner);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<CaravanPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var owner = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();

        settlement.Culture = culture;

        // Act
        string partyId = "TestId";
        client1.Call(() =>
        {
            CaravanPartyComponent.CreateCaravanParty(owner, settlement);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}
