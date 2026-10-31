using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions.Battles;
using Missions.Services.Network;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalPlayerHeroGraceTests
{
    private const BattleSideEnum Players = BattleSideEnum.Attacker;
    private const BattleSideEnum Others = BattleSideEnum.Defender;

    private readonly Mock<IBattleSession> session = new Mock<IBattleSession>();
    private readonly Mock<IMissionContext> missionContext = new Mock<IMissionContext>();
    private readonly Mock<IPlayerManager> playerManager = new Mock<IPlayerManager>();
    private readonly Mock<IObjectManager> objectManager = new Mock<IObjectManager>();
    private readonly List<string> otherMembers = new List<string>();

    public NavalPlayerHeroGraceTests()
    {
        session.SetupGet(s => s.OwnControllerId).Returns("A");
        missionContext.SetupGet(c => c.ControllersInMission).Returns(otherMembers);
    }

    // An AI side has no player hero, so its hulls out of action defeat it at once, as vanilla's enemy side.
    [Fact]
    public void ASideWithNoLiveMemberHeroSeen_HasNoGrace()
    {
        var grace = Grace();

        grace.ObserveLiveHumans(new[] { (Others, (BasicCharacterObject)Character("pirate")) }, now: 0f);

        Assert.True(grace.HasExpired(Others, now: 0f));
    }

    // A player's hero is matched by its character's registry id, which differs from the character's StringId.
    [Fact]
    public void ALiveMemberHero_HoldsItsSideUntilItIsDownForTwentySeconds()
    {
        var heroOfA = Hero("A", "main_hero");
        var grace = Grace();

        grace.ObserveLiveHumans(new[] { (Players, heroOfA) }, now: 10f);

        Assert.False(grace.HasExpired(Players, now: 30f));
        Assert.True(grace.HasExpired(Players, now: 30.5f));
    }

    // PvP: each side is held by its own players' heroes only.
    [Fact]
    public void EachSidesMemberHeroes_HoldOnlyTheirOwnSide()
    {
        var heroOfA = Hero("A", "hero_a");
        otherMembers.Add("B");
        var heroOfB = Hero("B", "hero_b");
        var grace = Grace();

        grace.ObserveLiveHumans(new[] { (Players, heroOfA) }, now: 0f);
        grace.ObserveLiveHumans(new[] { (Others, heroOfB) }, now: 15f);

        Assert.True(grace.HasExpired(Players, now: 25f));
        Assert.False(grace.HasExpired(Others, now: 25f));
    }

    // Two players on one side: the side stays held while either hero lives.
    [Fact]
    public void TwoMemberHeroesOnOneSide_HoldItWhileEitherLives()
    {
        var heroOfA = Hero("A", "hero_a");
        otherMembers.Add("B");
        var heroOfB = Hero("B", "hero_b");
        var grace = Grace();

        grace.ObserveLiveHumans(new[] { (Players, heroOfA), (Players, heroOfB) }, now: 0f);
        grace.ObserveLiveHumans(new[] { (Players, heroOfB) }, now: 15f);

        Assert.False(grace.HasExpired(Players, now: 30f));
    }

    // An offline player's hero fielded as AI is no member's, so it holds nothing.
    [Fact]
    public void ANonMembersHero_HoldsNothing()
    {
        Hero("A", "hero_a");
        var heroOfOffline = Hero("C", "hero_c");
        var grace = Grace();

        grace.ObserveLiveHumans(new[] { (Others, heroOfOffline) }, now: 0f);

        Assert.True(grace.HasExpired(Others, now: 0f));
    }

    private NavalPlayerHeroGrace Grace() =>
        new NavalPlayerHeroGrace(session.Object, missionContext.Object, playerManager.Object, objectManager.Object);

    // A registered player whose hero character has a registry id unlike its StringId, as CharacterObject ids are.
    private BasicCharacterObject Hero(string controllerId, string stringId)
    {
        var character = Character(stringId);
        string registryId = "CharacterObject_" + stringId;
        objectManager.Setup(m => m.TryGetObject(registryId, out character)).Returns(true);
        var player = new Player(controllerId, "Hero_" + stringId, "Party_" + stringId, "Clan_" + stringId, registryId);
        playerManager.Setup(m => m.TryGetPlayer(controllerId, out player)).Returns(true);
        return character;
    }

    private static CharacterObject Character(string stringId)
    {
        var character = (CharacterObject)RuntimeHelpers.GetUninitializedObject(typeof(CharacterObject));
        character.StringId = stringId;
        return character;
    }
}
