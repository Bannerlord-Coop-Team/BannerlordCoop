using Common.Util;
using GameInterface.Services.Armies;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

[Collection("CampaignCurrentCollection")]
public class PlayerSiegeTargetScoringTests
{
    private readonly PlayerSiegeTargetScoring scoring = new(Mock.Of<IPlayerManager>());

    [Fact]
    public void CalculateSettlementDefense_NoPlayers_PreservesNpcStrength()
    {
        var defenders = new[]
        {
            Defender(100f, countsAsMobileLord: true),
            Defender(200f),
            Defender(500f, isEligible: false)
        };

        SettlementDefenseScore score = scoring.CalculateSettlementDefense(defenders);

        Assert.Equal(300f, score.TotalStrength);
        Assert.Equal(100f, score.MobileLordStrength);
    }

    [Fact]
    public void CalculateSettlementDefense_OneControlledDefender_UsesVanillaPlayerWeights()
    {
        var defenders = new[]
        {
            Defender(100f, countsAsMobileLord: true),
            Defender(100f, countsAsMobileLord: true, isPlayerParty: true)
        };

        SettlementDefenseScore score = scoring.CalculateSettlementDefense(defenders);

        Assert.Equal(120f, score.TotalStrength);
        Assert.Equal(120f, score.MobileLordStrength);
    }

    [Fact]
    public void CalculateSettlementDefense_MultipleControlledDefenders_AppliesSettlementWeightOnce()
    {
        var defenders = new[]
        {
            Defender(100f),
            Defender(100f, isPlayerParty: true),
            Defender(100f, isPlayerParty: true)
        };

        SettlementDefenseScore score = scoring.CalculateSettlementDefense(defenders);

        Assert.Equal(160f, score.TotalStrength);
    }

    [Fact]
    public void CalculateSettlementDefense_ControlledPartyElsewhere_DoesNotAffectTarget()
    {
        MobileParty targetDefender = CreateParty(100f);
        MobileParty controlledPartyElsewhere = CreateParty(500f);
        var target = ObjectHelper.SkipConstructor<Settlement>();
        target._partiesCache = new MBList<MobileParty> { targetDefender };
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(manager => manager.Contains(controlledPartyElsewhere)).Returns(true);
        var targetScoring = new PlayerSiegeTargetScoring(playerManager.Object);

        SettlementDefenseScore score = targetScoring.CalculateSettlementDefense(target);

        Assert.Equal(100f, score.TotalStrength);
    }

    [Fact]
    public void CalculateSettlementDefense_PlayerWeightCanCrossVanillaPreSiegeThreshold()
    {
        const float attackingStrength = 250f;
        var defenders = new[]
        {
            Defender(100f),
            Defender(100f, isPlayerParty: true)
        };

        SettlementDefenseScore score = scoring.CalculateSettlementDefense(defenders);

        Assert.True(attackingStrength >= score.TotalStrength * 2f);
        Assert.False(attackingStrength >= 200f * 2f);
    }

    [Fact]
    public void ApplyPlayerSettlementDefense_AfterContainerTeardown_PreservesVanillaPlayerPresenceWeight()
    {
        Game previousGame = Game.Current;
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            var target = ObjectHelper.SkipConstructor<Settlement>();
            var otherSettlement = ObjectHelper.SkipConstructor<Settlement>();
            var mainHero = ObjectHelper.SkipConstructor<Hero>();
            var mainCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
            mainCharacter._heroObject = mainHero;
            mainHero._characterObject = mainCharacter;
            mainHero._stayingInSettlement = target;

            var game = ObjectHelper.SkipConstructor<Game>();
            game.PlayerTroop = mainCharacter;
            Game.Current = game;
            ContainerProvider.Clear();

            float totalStrength = 200f;
            float mobileLordStrength = 100f;
            PlayerSiegeTargetScoringPatches.ApplyPlayerSettlementDefense(
                target,
                ref totalStrength,
                ref mobileLordStrength);

            Assert.Equal(160f, totalStrength);
            Assert.Equal(80f, mobileLordStrength);

            mainHero._stayingInSettlement = otherSettlement;
            PlayerSiegeTargetScoringPatches.ApplyPlayerSettlementDefense(
                target,
                ref totalStrength,
                ref mobileLordStrength);

            Assert.Equal(160f, totalStrength);
            Assert.Equal(80f, mobileLordStrength);

            game.PlayerTroop = null!;
            PlayerSiegeTargetScoringPatches.ApplyPlayerSettlementDefense(
                target,
                ref totalStrength,
                ref mobileLordStrength);

            Assert.Equal(160f, totalStrength);
            Assert.Equal(80f, mobileLordStrength);
        }
        finally
        {
            Game.Current = previousGame;
            if (hadPreviousContainer)
                ContainerProvider.SetContainer(previousContainer);
            else
                ContainerProvider.Clear();
        }
    }

    [Fact]
    public void Transpiler_OverridesOnlySettlementDefenseTotals()
    {
        MethodInfo method = AccessTools.Method(
            typeof(DefaultTargetScoreCalculatingModel),
            nameof(DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction));
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method).ToList();
        List<CodeInstruction> patched = PlayerSiegeTargetScoringPatches.Transpiler(original).ToList();
        MethodInfo mainPartyGetter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty));

        Assert.Equal(
            original.Count(instruction => instruction.Calls(mainPartyGetter)),
            patched.Count(instruction => instruction.Calls(mainPartyGetter)));
        Assert.Single(patched, instruction =>
            instruction.operand is MethodInfo called &&
            called.DeclaringType == typeof(PlayerSiegeTargetScoringPatches) &&
            called.Name == "ApplyPlayerSettlementDefense");
    }

    [Fact]
    public void Transpiler_AlreadyPatched_ReturnsInstructionsUnchanged()
    {
        MethodInfo method = AccessTools.Method(
            typeof(DefaultTargetScoreCalculatingModel),
            nameof(DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction));
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method).ToList();
        List<CodeInstruction> patched = PlayerSiegeTargetScoringPatches.Transpiler(original).ToList();

        List<CodeInstruction> repatched = PlayerSiegeTargetScoringPatches.Transpiler(patched).ToList();

        Assert.True(patched.SequenceEqual(repatched));
    }

    private static SettlementDefenderScoreData Defender(
        float strength,
        bool isEligible = true,
        bool countsAsMobileLord = false,
        bool isPlayerParty = false,
        bool isLedByPlayerParty = false)
        => new(
            strength,
            isEligible,
            countsAsMobileLord,
            isPlayerParty,
            isLedByPlayerParty);

    private static MobileParty CreateParty(float strength)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        partyBase.MemberRoster = ObjectHelper.SkipConstructor<TroopRoster>();
        partyBase._lastEstimatedStrengthVersionNo = 0;
        partyBase._cachedEstimatedStrength = strength;
        party.Party = partyBase;
        party.Aggressiveness = 1f;
        return party;
    }
}
