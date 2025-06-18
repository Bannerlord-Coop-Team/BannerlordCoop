using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using TaleWorlds.Localization;

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
            var newParty = BanditPartyComponent.CreateBanditParty("TestId", clan, hideout, true);
            partyId = newParty.StringId;
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
            var isBossParty = false;
            partyComponent = new BanditPartyComponent(hideout, isBossParty);
        });

        Assert.NotNull(partyComponent);

        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}