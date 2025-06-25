using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class LordPartyComponentTests : SyncTestBase
{
    string ComponentId;
    public LordPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        ComponentId = TestEnvironment.CreateRegisteredObject<LordPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_LordPartyComponent_Fields()
    {
        Server.ObjectManager.TryGetObject(ComponentId, out LordPartyComponent component);
        component._leader = null;
        TestEnvironment.AssertReferenceField<LordPartyComponent, Hero>(nameof(LordPartyComponent._leader));
        TestEnvironment.AssertField<LordPartyComponent, int>(nameof(LordPartyComponent._wagePaymentLimit), 5);
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;
        Hero leaderhero = null;
        Hero newLeaderHero = null;

        server.Call(() =>
        {
            leaderhero = GameObjectCreator.CreateInitializedObject<Hero>();
            newLeaderHero = GameObjectCreator.CreateInitializedObject<Hero>();

            leaderhero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var spawnSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var newParty = LordPartyComponent.CreateLordParty(null, leaderhero, new Vec2(5, 5), 5, spawnSettlement, leaderhero);
            partyId = newParty.StringId;
        });

        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.NotNull(newLeaderHero);
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

        Hero leaderHero = null;
        server.Call(() =>
        {
            leaderHero = GameObjectCreator.CreateInitializedObject<Hero>();
        });


        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            partyComponent = new LordPartyComponent(leaderHero, leaderHero);
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
