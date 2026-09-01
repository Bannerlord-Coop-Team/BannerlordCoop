using Common.Util;
using GameInterface.Services.Companions.Patches.Disable;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Companions.Patches.Disable;

/// <summary>Verifies companion lifecycle replacement behavior.</summary>
public class CompanionsCampaignBehaviorPatchesTests
{
    [Fact]
    public void ShouldCullWanderer_PlayerSharesSettlement_ReturnsFalse()
    {
        Assert.False(CompanionsCampaignBehaviorPatches.ShouldCullWanderer(
            candidateExists: true,
            isHired: false,
            sharesSettlementWithPlayer: true));
    }

    [Fact]
    public void ShouldCullWanderer_UnwatchedCandidate_ReturnsTrue()
    {
        Assert.True(CompanionsCampaignBehaviorPatches.ShouldCullWanderer(
            candidateExists: true,
            isHired: false,
            sharesSettlementWithPlayer: false));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShouldCullWanderer_MissingOrHiredCandidate_ReturnsFalse(
        bool candidateExists,
        bool isHired)
    {
        Assert.False(CompanionsCampaignBehaviorPatches.ShouldCullWanderer(
            candidateExists,
            isHired,
            sharesSettlementWithPlayer: false));
    }

    [Fact]
    public void IsAnyPlayerAtSettlement_MatchingNullSettlements_ReturnsTrue()
    {
        var playerHero = CreateHero(Hero.CharacterStates.Active);

        Assert.True(CompanionsCampaignBehaviorPatches.IsAnyPlayerAtSettlement(
            settlement: null,
            playerHeroes: new[] { playerHero }));
    }

    [Fact]
    public void IsAnyPlayerAtSettlement_DifferentSettlement_ReturnsFalse()
    {
        var playerHero = CreateHero(Hero.CharacterStates.Active);
        playerHero._stayingInSettlement = ObjectHelper.SkipConstructor<Settlement>();

        Assert.False(CompanionsCampaignBehaviorPatches.IsAnyPlayerAtSettlement(
            ObjectHelper.SkipConstructor<Settlement>(),
            new[] { playerHero }));
    }

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
