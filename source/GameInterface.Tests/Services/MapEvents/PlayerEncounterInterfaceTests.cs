using Common.Util;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerEncounterInterfaceTests
{
    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;

    [Theory]
    [MemberData(nameof(ActiveLootScreens))]
    public void ShouldDeferAfterBattle_WhileLootScreenIsActive_ReturnsTrue(TaleWorlds.Core.GameState activeState)
    {
        Assert.True(PlayerEncounterPatches.ShouldDeferAfterBattle(activeState, isMapScreenTop: true));
    }

    public static TheoryData<TaleWorlds.Core.GameState> ActiveLootScreens => new()
    {
        new PartyState(),
        new InventoryState(),
    };

    [Fact]
    public void ShouldDeferAfterBattle_WhenMapIsActive_ReturnsFalse()
    {
        Assert.False(PlayerEncounterPatches.ShouldDeferAfterBattle(new MapState(), isMapScreenTop: true));
    }

    [Fact]
    public void ShouldDeferAfterBattle_WhenMapScreenIsNotTop_ReturnsTrue()
    {
        Assert.True(PlayerEncounterPatches.ShouldDeferAfterBattle(new MapState(), isMapScreenTop: false));
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_ForeignPlayerCompanion_ReturnsTrue()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var ownerClan = ObjectHelper.SkipConstructor<Clan>();
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._companionOf = ownerClan;
        playerObjects.Add(ownerClan, new ControlledObjectInfo("PlayerTwo", new ControllerIdProvider()));

        try
        {
            Assert.True(PlayerEncounterInterface.ShouldReleaseWithoutConversation(companion, localClan));
        }
        finally
        {
            playerObjects.Remove(ownerClan);
        }
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_ForeignPlayerHero_ReturnsTrue()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var ownerClan = ObjectHelper.SkipConstructor<Clan>();
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        playerHero._clan = ownerClan;
        playerObjects.Add(playerHero, new ControlledObjectInfo("PlayerTwo", new ControllerIdProvider()));

        try
        {
            Assert.True(PlayerEncounterInterface.ShouldReleaseWithoutConversation(playerHero, localClan));
        }
        finally
        {
            playerObjects.Remove(playerHero);
        }
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_LocalPlayerHero_ReturnsFalse()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        playerHero._clan = localClan;
        playerObjects.Add(playerHero, new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));

        try
        {
            Assert.False(PlayerEncounterInterface.ShouldReleaseWithoutConversation(playerHero, localClan));
        }
        finally
        {
            playerObjects.Remove(playerHero);
        }
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_AiLord_ReturnsFalse()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var aiClan = ObjectHelper.SkipConstructor<Clan>();
        var lord = ObjectHelper.SkipConstructor<Hero>();
        lord._clan = aiClan;

        Assert.False(PlayerEncounterInterface.ShouldReleaseWithoutConversation(lord, localClan));
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_LocalPlayerCompanion_ReturnsFalse()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._companionOf = localClan;
        playerObjects.Add(localClan, new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));

        try
        {
            Assert.False(PlayerEncounterInterface.ShouldReleaseWithoutConversation(companion, localClan));
        }
        finally
        {
            playerObjects.Remove(localClan);
        }
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_CompanionOfUnregisteredClan_ReturnsTrue()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var aiClan = ObjectHelper.SkipConstructor<Clan>();
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._companionOf = aiClan;

        Assert.True(PlayerEncounterInterface.ShouldReleaseWithoutConversation(companion, localClan));
    }

    [Fact]
    public void ShouldReleaseWithoutConversation_CompanionOfHeirLedFormerPlayerClan_ReturnsTrue()
    {
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new PlayerManager(new Mock<ILogger>().Object, objectManager.Object, new ControllerIdProvider());
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var formerPlayerClan = ObjectHelper.SkipConstructor<Clan>();
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var heir = ObjectHelper.SkipConstructor<Hero>();
        playerHero.OwnedCaravans = new();
        objectManager.Setup(o => o.TryGetObjectWithLogging("player-hero", out playerHero)).Returns(true);
        objectManager.Setup(o => o.TryGetObject("player-hero", out playerHero)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging("player-clan", out formerPlayerClan)).Returns(true);
        objectManager.Setup(o => o.TryGetObject("player-clan", out formerPlayerClan)).Returns(true);
        var player = new Player("PlayerTwo", "player-hero", string.Empty, "player-clan", string.Empty);
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._companionOf = formerPlayerClan;

        try
        {
            formerPlayerClan._leader = playerHero;
            Assert.True(playerManager.AddPlayer(player));
            Assert.True(formerPlayerClan.IsPlayerClan());

            // The player is deleted and an unregistered heir leads the clan.
            formerPlayerClan._leader = heir;
            Assert.True(playerManager.RemovePlayer(player));
            Assert.False(formerPlayerClan.IsPlayerClan());

            Assert.True(PlayerEncounterInterface.ShouldReleaseWithoutConversation(companion, localClan));
        }
        finally
        {
            playerManager.RemovePlayer(player);
        }
    }
}
