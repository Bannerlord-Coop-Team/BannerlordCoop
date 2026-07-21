using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerEncounterInterfaceTests
{
    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;

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
