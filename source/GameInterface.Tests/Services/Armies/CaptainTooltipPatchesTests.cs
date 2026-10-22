using Common.Util;
using GameInterface.Services.Armies.Patches;
using HarmonyLib;
using SandBox.ViewModelCollection;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

public class CaptainTooltipPatchesTests
{
    [Theory]
    [InlineData("Created_10", "Created_11")]
    [InlineData("captain", "captain")]
    public void ResolvesTheLinkedHeroRegardlessOfStringIds(string heroId, string characterId)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.StringId = heroId;
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        character.StringId = characterId;
        character.HeroObject = hero;
        var agent = ObjectHelper.SkipConstructor<Agent>();
        agent._character = character;

        var vanillaMatch = new[] { hero }.FirstOrDefault(candidate => candidate.StringId == character.StringId);
        if (heroId != characterId) Assert.Null(vanillaMatch);
        Assert.Same(hero, CaptainTooltipPatches.GetCaptainHero(agent));
    }

    [Fact]
    public void RegularTroopsHaveNoCaptainHero()
    {
        var agent = ObjectHelper.SkipConstructor<Agent>();
        agent._character = ObjectHelper.SkipConstructor<CharacterObject>();

        Assert.Null(CaptainTooltipPatches.GetCaptainHero(agent));
    }

    [Fact]
    public void ReplacesTheInstalledTooltipLookupAndPreservesVanillaCalculations()
    {
        var target = AccessTools.Method(typeof(SPOrderOfBattleVM), "GetAgentTooltip");
        var original = PatchProcessor.GetOriginalInstructions(target).ToList();
        var findHero = AccessTools.Method(typeof(Hero), nameof(Hero.FindFirst));
        Assert.Single(original.Where(instruction => instruction.Calls(findHero)));

        var patched = CaptainTooltipPatches.Transpiler(original).ToList();

        Assert.DoesNotContain(patched, instruction => instruction.Calls(findHero));
        Assert.Single(patched.Where(instruction => instruction.Calls(
            AccessTools.Method(typeof(CaptainTooltipPatches), nameof(CaptainTooltipPatches.GetCaptainHero)))));
        Assert.Equal(original.Count + 2, patched.Count);
    }
}
