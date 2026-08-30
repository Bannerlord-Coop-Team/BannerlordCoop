using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Commands;
using HarmonyLib;
using Helpers;
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
    public void IsRaidLootPartyState_WhenLootPartyScreenIsActive_ReturnsTrue()
    {
        var partyState = new PartyState
        {
            PartyScreenMode = PartyScreenHelper.PartyScreenMode.Loot
        };

        Assert.True(RaidDebugCommands.IsRaidLootPartyState(partyState));
    }

    [Fact]
    public void IsRaidLootPartyState_WhenNormalPartyScreenIsActive_ReturnsFalse()
    {
        var partyState = new PartyState
        {
            PartyScreenMode = PartyScreenHelper.PartyScreenMode.Normal
        };

        Assert.False(RaidDebugCommands.IsRaidLootPartyState(partyState));
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
    public void ShouldReleaseWithoutConversation_AiCompanion_ReturnsFalse()
    {
        var localClan = ObjectHelper.SkipConstructor<Clan>();
        var aiClan = ObjectHelper.SkipConstructor<Clan>();
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._companionOf = aiClan;

        Assert.False(PlayerEncounterInterface.ShouldReleaseWithoutConversation(companion, localClan));
    }
}
