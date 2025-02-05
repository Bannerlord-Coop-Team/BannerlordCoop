using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public HeroCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateHero_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        

        // Act
        Hero? serverHero = null;
        server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            MBObjectManager.Instance.RegisterObject(characterObject);

            var hero = HeroCreator.CreateSpecialHero(characterObject);

            hero.BornSettlement = Settlement.GetFirst;
            serverHero = hero;

            hero.SetName(new TextObject("Test Name"), new TextObject("Name"));
        });

        // Assert
        var newHeroStringId = serverHero?.StringId;
        Assert.NotNull(newHeroStringId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var newHero));

            Assert.Equal(serverHero?.FirstName.Value, newHero.FirstName.Value);
        }
    }

    [Fact]
    public void ClientCreateHero_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();
        
        // Act
        Hero? clientHero = null;
        client1.Call(() =>
        {
            var hero = new Hero();

            hero.BornSettlement = Settlement.GetFirst;
            clientHero = hero;
        });

        var newHeroStringId = clientHero?.StringId;

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var _));
        }
    }
}