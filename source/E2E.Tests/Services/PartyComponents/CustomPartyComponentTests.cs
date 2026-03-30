using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;
using GameInterface.Services.WeaponDesigns.Messages;
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
    string PartyId;
    public CustomPartyComponentTests(ITestOutputHelper output) : base(output)
    {
        PartyId = TestEnvironment.CreateRegisteredObject<CustomPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_CustomPartyComponent_Fields()
    {
        Server.ObjectManager.TryGetObject(PartyId, out CustomPartyComponent component);

        TestEnvironment.AssertField<CustomPartyComponent, TextObject>(nameof(CustomPartyComponent._name), new TextObject("name"));
        TestEnvironment.AssertReferenceField<CustomPartyComponent, Settlement>(nameof(CustomPartyComponent._homeSettlement));
        TestEnvironment.AssertReferenceField<CustomPartyComponent, Hero>(nameof(CustomPartyComponent._owner));
        TestEnvironment.AssertField<CustomPartyComponent, float>(nameof(CustomPartyComponent._customPartyBaseSpeed), 5f);
        TestEnvironment.AssertField<CustomPartyComponent, string>(nameof(CustomPartyComponent._partyMountStringId), "testMount", "");
        TestEnvironment.AssertField<CustomPartyComponent, string>(nameof(CustomPartyComponent._partyHarnessStringId), "testHarness", "");
        TestEnvironment.AssertField<CustomPartyComponent, bool>(nameof(CustomPartyComponent._avoidHostileActions), true);
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

            var newParty = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(new CampaignVec2(new Vec2(5, 5), true), 5, spawnSettlement, name, clan, partyTemplate, hero);
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
        Settlement settlement = new Settlement();
        Clan clan = new Clan();
        client1.Call(() =>
        {
            partyComponent = new CustomPartyComponent(settlement, 
                new TextObject(""), 
                new Hero(), 
                "test", 
                "testH", 
                1f, 
                false,
                new CustomPartyComponent.InitializationArgs(new CampaignVec2(new Vec2(2, 2), true), 2f, clan));
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}