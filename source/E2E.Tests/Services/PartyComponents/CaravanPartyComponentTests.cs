using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class CaravanPartyComponentTests : SyncTestBase
{
    string CaravanId;

    public CaravanPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        CaravanId = TestEnvironment.CreateRegisteredObject<CaravanPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_CaravanPartyComponent_Fields()
    {
        Server.ObjectManager.TryGetObject(CaravanId, out CaravanPartyComponent caravan);
        caravan._leader = null;

        TestEnvironment.AssertReferenceField<CaravanPartyComponent, Hero>(nameof(CaravanPartyComponent._leader));
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;
        Hero newLeaderHero = null;

        server.Call(() =>
        {
            var owner = GameObjectCreator.CreateInitializedObject<Hero>();
            newLeaderHero = GameObjectCreator.CreateInitializedObject<Hero>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            settlement.Culture = culture;
            var newParty = CaravanPartyComponent.CreateCaravanParty(owner, settlement, caravanLeader: owner);
            partyId = newParty.StringId;

        }, new MethodBase[]
        {
            AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty)),
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.NotNull(newLeaderHero);
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
        Hero hero = null;
        Settlement settlement = null;

        server.Call(() =>
        {
            hero = GameObjectCreator.CreateInitializedObject<Hero>();
            settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        });

            // Act
            PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            partyComponent = new CaravanPartyComponent(settlement, hero, hero);
        });

        Assert.NotNull(partyComponent);

        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
