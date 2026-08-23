using Common.Util;
using GameInterface.Services.HeroDevelopers.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.HeroDevelopers;

public class PerkActivationPatchTests
{
    [Fact]
    public void ShouldRefreshPlayerPartyRoster_PlayerParty_ReturnsTrueForAnyPerk()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        AccessTools.Property(typeof(Hero), nameof(Hero.PartyBelongedTo)).SetValue(hero, party);

        var playerObjects = (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools.Field(typeof(PlayerManager), "PlayerObjects").GetValue(null)!;
        playerObjects.Add(party, new ControlledObjectInfo("TestPlayer", null!));

        try
        {
            Assert.True(PerkActivationPatch.ShouldRefreshPlayerPartyRoster(hero));
        }
        finally
        {
            playerObjects.Remove(party);
        }
    }

    [Fact]
    public void ShouldRefreshPlayerPartyRoster_NonPlayerParty_ReturnsFalse()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        AccessTools.Property(typeof(Hero), nameof(Hero.PartyBelongedTo)).SetValue(hero, party);

        Assert.False(PerkActivationPatch.ShouldRefreshPlayerPartyRoster(hero));
    }
}