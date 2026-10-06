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
[Collection(ModInformationRoleCollection.Name)]
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

    [Fact]
    public void ShouldSpawnWanderer_FreeOnly_IgnoresHiredWanderers()
    {
        var template = new CharacterObject { _occupation = Occupation.Wanderer };
        var free = CreateWanderer(template);
        var hired = CreateHiredWanderer(template);

        // 1 free wanderer, so below a limit of 2 even though 2 wanderers exist
        Assert.True(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new[] { free, hired }, limit: 2, freeOnly: true));
        Assert.False(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new[] { free, hired }, limit: 1, freeOnly: true));
    }

    [Fact]
    public void ShouldSpawnWanderer_NotFreeOnly_CountsHiredWanderers()
    {
        var template = new CharacterObject { _occupation = Occupation.Wanderer };
        var free = CreateWanderer(template);
        var hired = CreateHiredWanderer(template);

        Assert.False(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new[] { free, hired }, limit: 2, freeOnly: false));
        Assert.True(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new[] { free, hired }, limit: 3, freeOnly: false));
    }

    [Fact]
    public void ShouldSpawnWanderer_NonWanderers_AreNotCounted()
    {
        var lord = CreateHero(Hero.CharacterStates.Active);
        var template = new CharacterObject { _occupation = Occupation.Wanderer };
        var wanderer = CreateWanderer(template);

        Assert.True(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new[] { lord, wanderer }, limit: 2, freeOnly: true));
        Assert.True(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new[] { lord, wanderer }, limit: 2, freeOnly: false));
    }

    [Fact]
    public void ShouldSpawnWanderer_NoHeroes_SpawnsWhenLimitPositive()
    {
        Assert.True(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new Hero[0], limit: 1, freeOnly: true));
        Assert.False(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            new Hero[0], limit: 0, freeOnly: true));
    }

    [Fact]
    public void ShouldSpawnWanderer_CountEqualsLimit_DoesNotSpawn()
    {
        var template = new CharacterObject { _occupation = Occupation.Wanderer };
        var heroes = new[] { CreateWanderer(template), CreateWanderer(template) };

        Assert.False(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            heroes, limit: 2, freeOnly: true));
    }

    [Fact]
    public void ShouldSpawnWanderer_FractionalLimit_ComparesAsFloat()
    {
        var template = new CharacterObject { _occupation = Occupation.Wanderer };
        var heroes = new[] { CreateWanderer(template), CreateWanderer(template) };

        Assert.True(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            heroes, limit: 2.5f, freeOnly: true));
        Assert.False(CompanionsCampaignBehaviorPatches.ShouldSpawnWanderer(
            heroes, limit: 1.5f, freeOnly: true));
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
        var candidate = CreateWanderer(template);
        candidate._stayingInSettlement = settlement;

        var behavior = new CompanionsCampaignBehavior();
        behavior._aliveCompanionTemplates.Add(template);
        return (behavior, candidate);
    }

    private static Hero CreateWanderer(CharacterObject template)
    {
        var generatedCharacter = new CharacterObject
        {
            _occupation = Occupation.Wanderer,
            _originCharacter = template,
        };
        var wanderer = CreateHero(Hero.CharacterStates.Active);
        wanderer._characterObject = generatedCharacter;
        wanderer.Occupation = Occupation.Wanderer;
        generatedCharacter._heroObject = wanderer;
        return wanderer;
    }

    private static Hero CreateHiredWanderer(CharacterObject template)
    {
        var wanderer = CreateWanderer(template);
        wanderer._companionOf = ObjectHelper.SkipConstructor<Clan>();
        return wanderer;
    }
}
