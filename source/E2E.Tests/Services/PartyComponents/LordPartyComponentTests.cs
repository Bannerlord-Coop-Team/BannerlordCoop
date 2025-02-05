using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
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

        var leaderField = AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._leader));
        var wageLimitField = AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._wagePaymentLimit));

        var leaderIntercept = TestEnvironment.GetIntercept(leaderField);
        var wageLimitIntercept = TestEnvironment.GetIntercept(wageLimitField);

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

            leaderIntercept.Invoke(null, new object[] { newParty.LordPartyComponent, newLeaderHero });
            wageLimitIntercept.Invoke(null, new object[] { newParty.LordPartyComponent, 5 });
        });

        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.NotNull(newLeaderHero);
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<LordPartyComponent>(newParty.PartyComponent);

            Assert.Equal(newLeaderHero.StringId, newParty.LordPartyComponent._leader.StringId);
            Assert.Equal(5, newParty.LordPartyComponent._wagePaymentLimit);
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
