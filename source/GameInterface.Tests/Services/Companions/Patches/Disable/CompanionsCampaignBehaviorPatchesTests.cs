using Common;
using Common.Util;
using GameInterface.Services.Companions.Patches.Disable;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Companions.Patches.Disable;

/// <summary>Verifies companion lifecycle replacement behavior.</summary>
public class CompanionsCampaignBehaviorPatchesTests
{
    [Fact]
    public void TryKillCompanionPrefix_Client_SuppressesVanillaBeforeReadingServerState()
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            Assert.False(CompanionsCampaignBehaviorPatches.TryKillCompanionPrefix(null));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void TryKillCompanionPrefix_ServerRandomGate_DoesNotCull()
    {
        var (behavior, candidate) = CreateCullCandidate();
        var removed = new List<Hero>();

        Assert.False(CompanionsCampaignBehaviorPatches.TryKillCompanionPrefix(
            behavior,
            randomFloat: 0.11f,
            getAliveHeroes: () => new[] { candidate },
            getPlayerHeroes: () => new[] { CreateHero(Hero.CharacterStates.Active) },
            removeWanderer: removed.Add));

        Assert.Empty(removed);
    }

    [Fact]
    public void TryKillCompanionPrefix_ServerWithoutTemplates_DoesNotCull()
    {
        var removed = new List<Hero>();

        Assert.False(CompanionsCampaignBehaviorPatches.TryKillCompanionPrefix(
            new CompanionsCampaignBehavior(),
            randomFloat: 0f,
            getAliveHeroes: () => new[] { CreateHero(Hero.CharacterStates.Active) },
            getPlayerHeroes: () => new[] { CreateHero(Hero.CharacterStates.Active) },
            removeWanderer: removed.Add));

        Assert.Empty(removed);
    }

    [Fact]
    public void TryKillCompanionPrefix_ServerCandidateWithoutSettlement_CullsWanderer()
    {
        var (behavior, candidate) = CreateCullCandidate();
        var playerHero = CreateHero(Hero.CharacterStates.Active);
        var removed = new List<Hero>();

        Assert.False(CompanionsCampaignBehaviorPatches.TryKillCompanionPrefix(
            behavior,
            randomFloat: 0f,
            getAliveHeroes: () => new[] { candidate },
            getPlayerHeroes: () => new[] { playerHero },
            removeWanderer: removed.Add));

        Assert.Equal(new[] { candidate }, removed);
    }

    [Fact]
    public void TryKillCompanionPrefix_ServerCandidateAtPlayerSettlement_DoesNotCull()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var (behavior, candidate) = CreateCullCandidate(settlement);
        var playerHero = CreateHero(Hero.CharacterStates.Active);
        playerHero._stayingInSettlement = settlement;
        var removed = new List<Hero>();

        Assert.False(CompanionsCampaignBehaviorPatches.TryKillCompanionPrefix(
            behavior,
            randomFloat: 0f,
            getAliveHeroes: () => new[] { candidate },
            getPlayerHeroes: () => new[] { playerHero },
            removeWanderer: removed.Add));

        Assert.Empty(removed);
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

    private static (CompanionsCampaignBehavior behavior, Hero candidate) CreateCullCandidate(
        Settlement settlement = null)
    {
        var template = new CharacterObject { _occupation = Occupation.Wanderer };
        var generatedCharacter = new CharacterObject
        {
            _occupation = Occupation.Wanderer,
            _originCharacter = template,
        };
        var candidate = CreateHero(Hero.CharacterStates.Active);
        candidate._characterObject = generatedCharacter;
        candidate.Occupation = Occupation.Wanderer;
        candidate._stayingInSettlement = settlement;
        generatedCharacter._heroObject = candidate;

        var behavior = new CompanionsCampaignBehavior();
        behavior._aliveCompanionTemplates.Add(template);
        return (behavior, candidate);
    }
}
