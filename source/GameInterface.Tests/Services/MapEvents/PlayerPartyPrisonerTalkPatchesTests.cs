using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests the Party Screen captured-player Talk gate.</summary>
public class PlayerPartyPrisonerTalkPatchesTests
{
    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;

    [Fact]
    public void ShouldBlockTalk_PlayerPrisoner_ReturnsTrue()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        playerObjects.Add(hero, new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));

        try
        {
            Assert.True(PlayerPartyPrisonerTalkPatches.ShouldBlockTalk(hero, isPrisoner: true));
        }
        finally
        {
            playerObjects.Remove(hero);
        }
    }

    [Fact]
    public void ShouldBlockTalk_PlayerPartyMember_ReturnsFalse()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        playerObjects.Add(hero, new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));

        try
        {
            Assert.False(PlayerPartyPrisonerTalkPatches.ShouldBlockTalk(hero, isPrisoner: false));
        }
        finally
        {
            playerObjects.Remove(hero);
        }
    }

    [Fact]
    public void ShouldBlockTalk_NonPlayerPrisoner_ReturnsFalse()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();

        Assert.False(PlayerPartyPrisonerTalkPatches.ShouldBlockTalk(hero, isPrisoner: true));
    }
}
