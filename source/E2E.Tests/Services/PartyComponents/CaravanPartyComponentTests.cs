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
    public CaravanPartyComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Server_CaravanPartyComponent_Fields()
    {
        var caravanId = TestEnvironment.CreateRegisteredObject<CaravanPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Hero>();

        Server.ObjectManager.TryGetObject(caravanId, out CaravanPartyComponent caravan);
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
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            settlement.Culture = culture;
            var newParty = CaravanPartyComponent.CreateCaravanParty(owner, settlement, template, caravanLeader: owner);

            Assert.True(server.ObjectManager.TryGetId(newParty, out partyId));

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
        PartyTemplateObject template = null;

        server.Call(() =>
        {
            hero = GameObjectCreator.CreateInitializedObject<Hero>();
            settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
        });

        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            var initArgs = new CaravanPartyComponent.InitializationArgs(template);
            partyComponent = new CaravanPartyComponent(settlement, hero, hero, false, initArgs);
        });

        Assert.NotNull(partyComponent);

        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
