using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class LordPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public LordPartyComponentTests(ITestOutputHelper output)
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

        var leaderHero = GameObjectCreator.CreateInitializedObject<Hero>();
        var spawnSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var newParty = LordPartyComponent.CreateLordParty(null, leaderHero, new Vec2(5, 5), 5, spawnSettlement, leaderHero);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<LordPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var leaderHero = GameObjectCreator.CreateInitializedObject<Hero>();
        var spawnSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        // Act
        string partyId = "TestId";
        client1.Call(() =>
        {
            LordPartyComponent.CreateLordParty(partyId, leaderHero, new Vec2(5, 5), 5, spawnSettlement, leaderHero);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}
