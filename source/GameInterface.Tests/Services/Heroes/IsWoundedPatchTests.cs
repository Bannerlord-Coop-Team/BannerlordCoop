using Common.Util;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System;
using GameInterface.Tests.Services.SiegeEvents;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

[Collection(nameof(CampaignCurrentCollection))]
public class IsWoundedPatchTests
{
    private static bool _isPlayerHero = true;

    [Fact]
    public void IsWounded_NonPlayerCharacter_ReturnsOriginal()
    {
        var harmony = new Harmony($"{nameof(IsWoundedPatchTests)}.{Guid.NewGuid():N}");
        var nonPlayerHero = ObjectHelper.SkipConstructor<Hero>();

        try
        {
            _isPlayerHero = false;
            PatchIsHumanPlayerCharacter(harmony);

            bool retVal = false;
            var runVanilla = HeroPatches.IsWoundedPrefix(nonPlayerHero, ref retVal);
            

            // We do not care about the return value for non-players.
            Assert.True(runVanilla);
        }
        finally
        {
            Cleanup(harmony);
        }
    }

    [Fact]
    public void IsWounded_HumanPlayerCharacter_ReturnsFalse()
    {
        var harmony = new Harmony($"{nameof(IsWoundedPatchTests)}.{Guid.NewGuid():N}");
        var playerHero = ObjectHelper.SkipConstructor<Hero>();

        try
        {
            // flag true for player test
            _isPlayerHero = true;

            // Also patch IsHumanPlayerCharacter to return true for players
            PatchIsHumanPlayerCharacter(harmony);

            var retVal = true;
            var runVanilla = HeroPatches.IsWoundedPrefix(playerHero, ref retVal);

            Assert.False(retVal);
            Assert.False(runVanilla);
        }
        finally
        {
            Cleanup(harmony);
        }
    }

    private static void Cleanup(Harmony harmony)
    {
        _isPlayerHero = true;
        harmony.UnpatchAll(harmony.Id);
    }

    private static void PatchIsHumanPlayerCharacter(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.IsHumanPlayerCharacter)),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(IsWoundedPatchTests),
                    nameof(GetIsHumanPlayerCharacterPrefix)), 
                // required because somewhere in a previous test patches are done to the property
                Priority.First));
    }

    private static bool GetIsHumanPlayerCharacterPrefix(Hero __instance, ref bool __result)
    {
        __result = _isPlayerHero;
        return false;  // Prevent original method from running
    }
    
}
