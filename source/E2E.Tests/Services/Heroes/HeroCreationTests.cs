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

public class HeroCreationTests
{
    E2ETestEnvironement TestEnvironement { get; }
    public HeroCreationTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironement(output);
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
    public void ClineSetName_DoesNothing()
    {
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        var networkId = "CoopHero_1";
        var serverHero = TestEnvironement.Server.CreateRegisteredObject<Hero>(networkId);
        var clientHeroes = new List<Hero>();

        foreach (var client in TestEnvironement.Clients)
        {
            clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
        }

        var originalFullName = new TextObject("Test Name");
        var originalFirstName = new TextObject("Name");

        server.Call(() =>
        {
            serverHero.SetName(originalFullName, originalFirstName);
        });

        var differentFullName = new TextObject("Dont set me");
        var differentFirstName = new TextObject("Dont set me");

        client1.Call(() =>
        {
            clientHeroes.First().SetName(differentFullName, differentFirstName);
        });

        foreach (var clientHero in clientHeroes)
        {
            Assert.Equal(originalFullName.Value, clientHero.Name.Value);
            Assert.Equal(originalFirstName.Value, clientHero.FirstName.Value);

            Assert.NotEqual(differentFullName.Value, clientHero.Name.Value);
            Assert.NotEqual(differentFirstName.Value, clientHero.FirstName.Value);
        }
    }
}