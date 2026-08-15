using E2E.Tests.Util;
using HarmonyLib;
using GameInterface.Services.PartyComponents.Messages;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
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
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
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
            Assert.True(newParty.IsCaravan);
            Assert.False(newParty.IsLordParty);
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

    [Fact]
    public void ServerCreateParty_CaravanSeedItemsSyncByValue()
    {
        var server = TestEnvironment.Server;
        string? partyId = null;
        ItemRoster? seedItems = null;
        PartyTemplateObject? template = null;

        server.Call(() =>
        {
            template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            Assert.True(server.ObjectManager.AddExisting("caravan_seed_template", template));
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                var clientTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
                Assert.True(client.ObjectManager.AddExisting("caravan_seed_template", clientTemplate));
            });
        }

        server.Call(() =>
        {
            var owner = GameObjectCreator.CreateInitializedObject<Hero>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var mule = MBObjectManager.Instance.GetObject<ItemObject>("mule");

            Assert.NotNull(mule);
            settlement.Culture = culture;
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());

            seedItems = new ItemRoster();
            seedItems.AddToCounts(mule, 3);

            var newParty = CaravanPartyComponent.CreateCaravanParty(
                owner,
                settlement,
                template,
                caravanLeader: owner,
                caravanItems: seedItems);

            Assert.True(server.ObjectManager.TryGetId(newParty, out partyId));
            Assert.False(server.ObjectManager.TryGetId(seedItems, out _));
        }, new MethodBase[]
        {
            AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty)),
        });

        Assert.NotNull(partyId);
        Assert.NotNull(seedItems);
        var sentInitMessage = Assert.Single(
            server.NetworkSentMessages.GetMessages<NetworkUpdateCaravanPartyComponentInitArgs>(),
            message => message.CaravanItems?.Length == 1);
        Assert.Equal(3, sentInitMessage.CaravanItems[0].Amount);

        foreach (var client in TestEnvironment.Clients)
        {
            var receivedInitMessage = Assert.Single(
                client.InternalMessages.GetMessages<NetworkUpdateCaravanPartyComponentInitArgs>(),
                message => message.CaravanItems?.Length == 1);
            Assert.Equal(3, receivedInitMessage.CaravanItems[0].Amount);
            Assert.Equal("mule", receivedInitMessage.CaravanItems[0].EquipmentElement.Item.StringId);
        }
    }

    [Fact]
    public void ClientReceiveCaravanSeedItems_ReconstructsTransientRoster()
    {
        var client = TestEnvironment.Clients.First();
        client.CreateRegisteredObject<CaravanPartyComponent>("caravan_component");
        client.CreateRegisteredObject<PartyTemplateObject>("caravan_template");

        client.Call(() =>
        {
            var mule = MBObjectManager.Instance.GetObject<ItemObject>("mule");
            Assert.NotNull(mule);

            client.SimulateMessage(this, new NetworkUpdateCaravanPartyComponentInitArgs(
                "caravan_component",
                null,
                new[] { new ItemRosterElement(mule, 3) },
                "caravan_template"));

            Assert.True(client.ObjectManager.TryGetObject<CaravanPartyComponent>("caravan_component", out var component));
            Assert.NotNull(component._initializationArgs);
            Assert.NotNull(component._initializationArgs.CaravanItems);
            Assert.False(client.ObjectManager.TryGetId(component._initializationArgs.CaravanItems, out _));

            var index = component._initializationArgs.CaravanItems.FindIndexOfItem(mule);
            Assert.True(index >= 0);
            Assert.Equal(3, component._initializationArgs.CaravanItems.GetElementNumber(index));
        });
    }
}
