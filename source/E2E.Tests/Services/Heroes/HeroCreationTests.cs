using Common.Messaging;
using Common.Tests.Utils;
using Common.Util;
using Coop.IntegrationTests.Environment.Instance;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroCreationTests : IDisposable
{
    E2ETestEnvironement TestEnvironement { get; }
    public HeroCreationTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironement(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerCreateHero_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        MBObjectManager.Instance.RegisterObject(characterObject);

        // Act
        Hero? serverHero = null;
        server.Call(() =>
        {
            var hero = HeroCreator.CreateSpecialHero(characterObject);

            hero.BornSettlement = Settlement.GetFirst;
            serverHero = hero;

            hero.SetName(new TextObject("Test Name"), new TextObject("Name"));
        });

        // Assert
        var newHeroStringId = serverHero?.StringId;
        Assert.NotNull(newHeroStringId);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var newHero));

            Assert.Equal(serverHero?.FirstName.Value, newHero.FirstName.Value);
        }
    }

    [Fact]
    public void ClientCreateHero_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        MBObjectManager.Instance.RegisterObject(characterObject);

        // Act
        Hero? clientHero = null;
        client1.Call(() =>
        {
            var hero = HeroCreator.CreateSpecialHero(characterObject);

            hero.BornSettlement = Settlement.GetFirst;
            clientHero = hero;
        });

        var newHeroStringId = clientHero?.StringId;

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var _));

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var _));
        }
    }
}