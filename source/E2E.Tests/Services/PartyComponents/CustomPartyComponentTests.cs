using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class CustomPartyComponentTests : SyncTestBase
{
    public CustomPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<CustomPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_CustomPartyComponent_Fields()
    {
        TestEnvironment.AssertReferenceProperty<CustomPartyComponent, TextObject>(nameof(CustomPartyComponent._name));
        TestEnvironment.AssertReferenceProperty<CustomPartyComponent, Settlement>(nameof(CustomPartyComponent._homeSettlement));
        TestEnvironment.AssertReferenceProperty<CustomPartyComponent, Hero>(nameof(CustomPartyComponent._owner));
        TestEnvironment.AssertProperty<CustomPartyComponent, float>(nameof(CustomPartyComponent._customPartyBaseSpeed), 5f);
        TestEnvironment.AssertProperty<CustomPartyComponent, string>(nameof(CustomPartyComponent._partyMountStringId), "testMount");
        TestEnvironment.AssertProperty<CustomPartyComponent, string>(nameof(CustomPartyComponent._partyHarnessStringId), "testHarness");
        TestEnvironment.AssertProperty<CustomPartyComponent, bool>(nameof(CustomPartyComponent._avoidHostileActions), true);
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
            var spawnSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            var name = new TextObject("Name");
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var partyTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();

            var newParty = CustomPartyComponent.CreateQuestParty(new Vec2(5, 5), 5, spawnSettlement, name, clan, partyTemplate, hero);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<CustomPartyComponent>(newParty.PartyComponent);
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
            partyComponent = new CustomPartyComponent();
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}