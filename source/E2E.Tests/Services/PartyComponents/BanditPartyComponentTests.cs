using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using TaleWorlds.Localization;
using TaleWorlds.Library;

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