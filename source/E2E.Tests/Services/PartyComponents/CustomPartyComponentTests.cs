using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class CustomPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public CustomPartyComponentTests(ITestOutputHelper output)
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
