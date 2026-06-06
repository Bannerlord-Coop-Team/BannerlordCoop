using Common.Messaging;
using GameInterface.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using Moq;
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
        Hero? hero = null;
        var registry = new ControlledEntityRegistry();
        var objectManagerMock = new Mock<IObjectManager>();

        objectManagerMock
        .Setup(x => x.TryGetObject<Hero>(It.IsAny<string>(), out hero))
        .Returns(false);

        var heroInterface = CreateHeroInterface(registry, objectManagerMock);

        // Act
        var result = heroInterface.TryResolve<Hero>(ControllerId, out var heroId);

        // Assert
        Assert.False(result);
        Assert.Null(heroId);
    }

    [Fact]
    public void TryResolveHero_EntitiesExistButNoneAreHeroes_ReturnsFalse()
    {
        // Arrange — controllerId has a party entity registered, but no Hero
        Hero? hero = null;
        var registry = new ControlledEntityRegistry();
        registry.RegisterAsControlled(ControllerId, "Coop_MobileParty_1");

        // ObjectManager knows about the party but not any Hero
        var objectManagerMock = new Mock<IObjectManager>();

        objectManagerMock
        .Setup(x => x.TryGetObject<Hero>(It.IsAny<string>(), out hero))
        .Returns(false);

        var heroInterface = CreateHeroInterface(registry, objectManagerMock);

        // Act
        var result = heroInterface.TryResolve<Hero>(ControllerId, out var heroId);

        // Assert
        Assert.False(result);
        Assert.Null(heroId);
    }

    [Fact]
    public void TryResolveHero_OneHeroRegistered_ReturnsTrueWithHeroId()
    {
        // Arrange — the normal reconnect case: one hero registered under the controllerId
        var registry = new ControlledEntityRegistry();
        var hero = CreateHeroStub();
        var heroId = "Coop_TaleWorlds.CampaignSystem.Hero_1";
        registry.RegisterAsControlled(ControllerId, heroId);

        // ObjectManager returns a Hero for that ID, matching what AutoRegistry<Hero> produces
        var objectManagerMock = new Mock<IObjectManager>();

        objectManagerMock
        .Setup(x => x.TryGetObject<Hero>(heroId, out hero))
        .Returns(true);

        var heroInterface = CreateHeroInterface(registry, objectManagerMock);

        // Act
        var result = heroInterface.TryResolve<Hero>(ControllerId, out var resolvedId);

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
        var hero = CreateHeroStub();
        var heroId = "Coop_TaleWorlds.CampaignSystem.Hero_1";
        var partyId = "Coop_MobileParty_1";
        registry.RegisterAsControlled(ControllerId, heroId);
        registry.RegisterAsControlled(ControllerId, partyId);

        // ObjectManager only resolves the hero entity as a Hero — the party entity is not a Hero
        var objectManagerMock = new Mock<IObjectManager>();

        objectManagerMock
        .Setup(x => x.TryGetObject<Hero>(heroId, out hero))
        .Returns(true);

        var heroInterface = CreateHeroInterface(registry, objectManagerMock);

        // Act
        var result = heroInterface.TryResolve<Hero>(ControllerId, out var resolvedId);

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

        var hero1 = CreateHeroStub();
        var heroId1 = "Coop_TaleWorlds.CampaignSystem.Hero_1";

        var hero2 = CreateHeroStub();
        var heroId2 = "Coop_TaleWorlds.CampaignSystem.Hero_2";
        registry.RegisterAsControlled(ControllerId, heroId1);
        registry.RegisterAsControlled(ControllerId, heroId2);

        var objectManagerMock = new Mock<IObjectManager>();

        objectManagerMock
        .Setup(x => x.TryGetObject<Hero>(heroId1, out hero1))
        .Returns(true);

        objectManagerMock
        .Setup(x => x.TryGetObject<Hero>(heroId2, out hero2))
        .Returns(true);

        var heroInterface = CreateHeroInterface(registry, objectManagerMock);

        // Act
        var result = heroInterface.TryResolve<Hero>(ControllerId, out var resolvedId);

        // Assert — no exception, first hero returned
        Assert.True(result);
        Assert.Equal(heroId1, resolvedId);
    }

    private static HeroInterface CreateHeroInterface(ControlledEntityRegistry registry, Mock<IObjectManager>? objectManagerMock = null)
    {
        var messageBrokerMock = new Mock<IMessageBroker>();
        var binaryPackageFactoryMock = new Mock<IBinaryPackageFactory>();

        objectManagerMock ??= new Mock<IObjectManager>();

        return new HeroInterface(messageBrokerMock.Object, binaryPackageFactoryMock.Object, registry, objectManagerMock.Object);
    }
}
