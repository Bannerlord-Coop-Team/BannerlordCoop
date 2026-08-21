using Common.Util;
using GameInterface.Services.Companions.Patches.Disable;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Companions.Patches.Disable;

/// <summary>Verifies stale prisoner repair behavior.</summary>
public class CompanionsCampaignBehaviorPatchesTests
{
    [Fact]
    public void RepairStuckHeroes_MultipleStuckHeroes_RepairsEveryMatch()
    {
        var firstStuckHero = CreateHero(Hero.CharacterStates.Prisoner);
        var validPrisoner = CreateHero(Hero.CharacterStates.Prisoner);
        validPrisoner.PartyBelongedToAsPrisoner = ObjectHelper.SkipConstructor<PartyBase>();
        var activeHero = CreateHero(Hero.CharacterStates.Active);
        var secondStuckHero = CreateHero(Hero.CharacterStates.Prisoner);
        var repairedHeroes = new List<Hero>();

        CompanionsCampaignBehaviorPatches.RepairStuckHeroes(
            new[] { firstStuckHero, validPrisoner, activeHero, secondStuckHero },
            repairedHeroes.Add);

        Assert.Equal(new[] { firstStuckHero, secondStuckHero }, repairedHeroes);
    }

    private static Hero CreateHero(Hero.CharacterStates state)
    {
        var hero = new Hero();
        hero._heroState = state;
        return hero;
    }
}
