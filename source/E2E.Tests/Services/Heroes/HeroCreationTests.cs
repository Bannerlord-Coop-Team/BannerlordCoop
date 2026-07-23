using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
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

            // Required: SetupMainHero also initializes these to avoid NullReferenceException in SetInitialValuesFromCharacter
            characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();

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
    public void ServerCreateBareHero_PreservesDefaultHealthOnClients()
    {
        Hero? serverHero = null;

        TestEnvironment.Server.Call(() => serverHero = new Hero());
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverHero!, out var heroId));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var clientHero));
            Assert.Equal(1, clientHero._health);
        }
    }

    [Fact]
    public void ServerCreateHeroes_ClientHeroesGetUniqueNonZeroIds()
    {
        // Arrange
        var server = TestEnvironment.Server;

        Hero? serverHero1 = null;
        Hero? serverHero2 = null;

        // Act
        server.Call(() =>
        {
            serverHero1 = new Hero();
            serverHero2 = new Hero();
        });

        Assert.True(server.ObjectManager.TryGetId(serverHero1!, out var heroId1));
        Assert.True(server.ObjectManager.TryGetId(serverHero2!, out var heroId2));

        // Assert
        // MBObjectBase.GetHashCode is Id-based. Client-created heroes used to skip vanilla
        // AddHero's MBGUID assignment (OnHeroAdded was called directly), leaving Id == 0 for
        // every synced hero: all of them collided in any Id-hashed dictionary, and a later Id
        // assignment would strand existing entries under the stale hash - the defect that
        // leaked dead-party nameplates (see MobilePartyRegistry.OnClientCreated).
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId1, out var clientHero1));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId2, out var clientHero2));

            Assert.NotEqual(0u, clientHero1.Id.InternalValue);
            Assert.NotEqual(0u, clientHero2.Id.InternalValue);
            Assert.NotEqual(clientHero1.Id, clientHero2.Id);
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
