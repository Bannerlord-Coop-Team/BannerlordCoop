using GameInterface.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes.Interfaces;

/// <summary>
/// Tests for <see cref="HeroInterface.TryResolveHero"/>.
///
/// TryResolveHero is the method called by the server when a client reconnects to check
/// whether that client already has a hero. It looks up the client's controllerId in the
/// ControlledEntityRegistry, filters to entities that resolve to a Hero in the ObjectManager,
/// and returns the first match.
///
/// These tests do not require the game to be running — all dependencies are either direct
/// instantiations (ControlledEntityRegistry) or lightweight stubs.
/// </summary>
public class HeroInterfaceTests
{
    // HeroInterface requires IBinaryPackageFactory only for PackageMainHero/UnpackHero.
    // TryResolveHero does not touch it, so we pass null safely.
    private static readonly IBinaryPackageFactory? NullPackageFactory = null;

    private const string ControllerId = "testclient1";

    /// <summary>
    /// Creates a Hero instance without invoking its constructor, avoiding any game
    /// bootstrap dependency. The instance only needs to exist so the ObjectManager stub
    /// can return it for a given ID.
    /// </summary>
    private static Hero CreateHeroStub() =>
        (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

    [Fact]
    public void TryResolveHero_NoEntitiesForController_ReturnsFalse()
    {
        // Arrange — registry has no entries for this controllerId at all
        var registry = new ControlledEntityRegistry();
        var objectManager = new TestObjectManager();
        var heroInterface = new HeroInterface(NullPackageFactory, registry, objectManager);

        // Act
        var result = heroInterface.TryResolveHero(ControllerId, out var heroId);

        // Assert
        Assert.False(result);
        Assert.Null(heroId);
    }

    [Fact]
    public void TryResolveHero_EntitiesExistButNoneAreHeroes_ReturnsFalse()
    {
        // Arrange — controllerId has a party entity registered, but no Hero
        var registry = new ControlledEntityRegistry();
        registry.RegisterAsControlled(ControllerId, "Coop_MobileParty_1");

        // ObjectManager knows about the party but not any Hero
        var objectManager = new TestObjectManager();

        var heroInterface = new HeroInterface(NullPackageFactory, registry, objectManager);

        // Act
        var result = heroInterface.TryResolveHero(ControllerId, out var heroId);

        // Assert
        Assert.False(result);
        Assert.Null(heroId);
    }

    [Fact]
    public void TryResolveHero_OneHeroRegistered_ReturnsTrueWithHeroId()
    {
        // Arrange — the normal reconnect case: one hero registered under the controllerId
        var registry = new ControlledEntityRegistry();
        var heroId = "Coop_TaleWorlds.CampaignSystem.Hero_1";
        registry.RegisterAsControlled(ControllerId, heroId);

        // ObjectManager returns a Hero for that ID, matching what AutoRegistry<Hero> produces
        var objectManager = new TestObjectManager();
        objectManager.RegisterHero(heroId, CreateHeroStub());

        var heroInterface = new HeroInterface(NullPackageFactory, registry, objectManager);

        // Act
        var result = heroInterface.TryResolveHero(ControllerId, out var resolvedId);

        // Assert
        Assert.True(result);
        Assert.Equal(heroId, resolvedId);
    }

    [Fact]
    public void TryResolveHero_HeroAndPartyBothRegistered_ReturnsTrueWithHeroId()
    {
        // Arrange — realistic case: a client has both a hero entity and a mobile party
        // entity registered under their controllerId. TryResolveHero must return only the hero.
        var registry = new ControlledEntityRegistry();
        var heroId = "Coop_TaleWorlds.CampaignSystem.Hero_1";
        var partyId = "Coop_MobileParty_1";
        registry.RegisterAsControlled(ControllerId, heroId);
        registry.RegisterAsControlled(ControllerId, partyId);

        // ObjectManager only resolves the hero entity as a Hero — the party entity is not a Hero
        var objectManager = new TestObjectManager();
        objectManager.RegisterHero(heroId, CreateHeroStub());

        var heroInterface = new HeroInterface(NullPackageFactory, registry, objectManager);

        // Act
        var result = heroInterface.TryResolveHero(ControllerId, out var resolvedId);

        // Assert
        Assert.True(result);
        Assert.Equal(heroId, resolvedId);
    }

    [Fact]
    public void TryResolveHero_MultipleHeroEntities_ReturnsTrueWithFirstHeroId()
    {
        // Arrange — defensive guard: two hero entities registered under the same controllerId.
        // This should not happen in normal gameplay (only the player's main hero is ever
        // registered under their controllerId), but the registry data model allows it.
        // The method should not throw and should return the first match.
        var registry = new ControlledEntityRegistry();
        var heroId1 = "Coop_TaleWorlds.CampaignSystem.Hero_1";
        var heroId2 = "Coop_TaleWorlds.CampaignSystem.Hero_2";
        registry.RegisterAsControlled(ControllerId, heroId1);
        registry.RegisterAsControlled(ControllerId, heroId2);

        var objectManager = new TestObjectManager();
        objectManager.RegisterHero(heroId1, CreateHeroStub());
        objectManager.RegisterHero(heroId2, CreateHeroStub());

        var heroInterface = new HeroInterface(NullPackageFactory, registry, objectManager);

        // Act
        var result = heroInterface.TryResolveHero(ControllerId, out var resolvedId);

        // Assert — no exception, first hero returned
        Assert.True(result);
        Assert.Equal(heroId1, resolvedId);
    }

    /// <summary>
    /// Minimal IObjectManager stub for hero resolution tests.
    /// Only implements TryGetObject&lt;T&gt; — the sole method used by TryResolveHero.
    /// </summary>
    private class TestObjectManager : IObjectManager
    {
        private readonly Dictionary<string, Hero> _heroes = new();

        public void RegisterHero(string id, Hero hero) => _heroes[id] = hero;

        public bool TryGetObject<T>(string id, out T obj) where T : class
        {
            if (typeof(T) == typeof(Hero) && _heroes.TryGetValue(id, out var hero))
            {
                obj = (hero as T)!;
                return obj != null;
            }

            obj = null!;
            return false;
        }

        // Remaining interface members are not exercised by TryResolveHero
        public bool AddExisting<T>(string id, T obj) => throw new NotImplementedException();
        public bool AddNewObject<T>(T obj, out string newId) => throw new NotImplementedException();
        public bool Contains<T>(T obj) => throw new NotImplementedException();
        public bool Contains(string id) => throw new NotImplementedException();
        public bool IsTypeManaged(Type type) => throw new NotImplementedException();
        public bool Remove<T>(T obj) => throw new NotImplementedException();
        public bool TryGetId<T>(T obj, out string id) => throw new NotImplementedException();

        public bool TryGetIdWithLogging<T>(T obj, out string id)
        {
            throw new NotImplementedException();
        }

        public bool TryGetObjectWithLogging<T>(string id, out T obj) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
