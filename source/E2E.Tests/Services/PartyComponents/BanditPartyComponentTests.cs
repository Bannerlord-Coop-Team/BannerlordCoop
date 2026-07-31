using E2E.Tests.Util;
using GameInterface.Services.Bandits;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class BanditPartyComponentTests : SyncTestBase
{
    public BanditPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<BanditPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Hideout>();
    }

    [Fact]
    public void Server_BanditPartyComponent_Properties()
    {
        TestEnvironment.AssertProperty<BanditPartyComponent, bool>(nameof(BanditPartyComponent.IsBossParty), true);
        TestEnvironment.AssertReferenceProperty<BanditPartyComponent, Hideout>(nameof(BanditPartyComponent.Hideout));
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            var newParty = BanditPartyComponent.CreateBanditParty("TestId", clan, hideout, true, template, new CampaignVec2(new Vec2(2, 2), true));

            Assert.True(server.ObjectManager.TryGetId(newParty, out partyId));
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<BanditPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ServerCreateLooterParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;
        string? clanId = null;
        string? settlementId = null;

        server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();

            // Mirrors BanditSpawnCampaignBehavior's looter spawn: the settlement ctor
            // (no hideout) plus InitializationArgs carrying the clan that OnMobilePartySetOnCreation
            // writes into MobileParty.ActualClan - the field BanditPartyComponent.Name resolves
            // the display name through (MapFaction.Name).
            var newParty = BanditPartyComponent.CreateLooterParty("TestLooterId", clan, settlement, false, template, new CampaignVec2(new Vec2(2, 2), true));

            Assert.True(server.ObjectManager.TryGetId(newParty, out partyId));
            Assert.True(server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(server.ObjectManager.TryGetId(settlement, out settlementId));
            Assert.Same(clan, newParty.ActualClan);
            Assert.Same(settlement, newParty.HomeSettlement);
        });

        // Assert
        Assert.NotNull(partyId);
        Assert.NotNull(clanId);
        Assert.NotNull(settlementId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));

            // Component link: without it MobileParty.Name falls back to "unnamedMobileParty".
            Assert.NotNull(newParty.PartyComponent);
            Assert.IsType<BanditPartyComponent>(newParty.PartyComponent);

            // Name source: BanditPartyComponent.Name = MobileParty.MapFaction.Name, and
            // MapFaction resolves via ActualClan for bandit parties.
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var clientClan));
            Assert.Same(clientClan, newParty.ActualClan);

            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var clientSettlement));
            Assert.Same(clientSettlement, newParty.HomeSettlement);
        }
    }

    [Fact]
    public void RepairMissingHomeSettlements_WhenLooterSaveIsCorrupted_UsesNearestTown()
    {
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            var nearSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            nearSettlement.Town = GameObjectCreator.CreateInitializedObject<Town>();
            nearSettlement._position = new CampaignVec2(new Vec2(10, 10), true);

            var farSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            farSettlement.Town = GameObjectCreator.CreateInitializedObject<Town>();
            farSettlement._position = new CampaignVec2(new Vec2(100, 100), true);

            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            var party = BanditPartyComponent.CreateLooterParty(
                "CorruptedLooter",
                clan,
                nearSettlement,
                false,
                template,
                new CampaignVec2(new Vec2(11, 11), true));
            var component = Assert.IsType<BanditPartyComponent>(party.PartyComponent);

            ReflectionUtils.SetPrivateField(
                typeof(BanditPartyComponent),
                nameof(BanditPartyComponent._relatedSettlement),
                component,
                null);
            Assert.Null(party.HomeSettlement);

            int repairedCount = server.Resolve<IBanditPartyHomeSettlementRepairer>()
                .RepairMissingHomeSettlements(
                    new[] { party },
                    new[] { farSettlement, nearSettlement });

            Assert.Equal(1, repairedCount);
            Assert.Same(nearSettlement, party.HomeSettlement);
        });
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            var isBossParty = false;
            var initArgs = new BanditPartyComponent.InitializationArgs(clan, template, new CampaignVec2(new Vec2(2, 2), true));
            
            partyComponent = new BanditPartyComponent(hideout, isBossParty, initArgs);
        });

        Assert.NotNull(partyComponent);

        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
