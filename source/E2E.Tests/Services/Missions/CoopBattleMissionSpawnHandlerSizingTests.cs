using Missions.Battles;
using GameInterface.Services.MapEvents.TroopSupply;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Game-independent tests for <see cref="CoopBattleMissionSpawnHandler.SideSizing"/>: sizing waits until both
/// reserves land (the joint cap needs both totals) and only runs Init on a positive combined total.
/// </summary>
public class CoopBattleMissionSpawnHandlerSizingTests
{
    [Fact]
    public void OneSideNotPopulated_NotReady_HoldsBothSides()
    {
        // Own side already has its reserve but the enemy side's (empty) reserve is still in flight: not ready,
        // so both sides stay held at zero until the second reserve lands.
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: false, defenderOwned: 7, attackerOwned: 0);

        Assert.False(sizing.Ready);
        Assert.False(sizing.SizeNow);
        Assert.True(sizing.HasAnyOwnedTroops);
    }

    [Fact]
    public void NeitherPopulated_NotReady()
    {
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: false, attackerPopulated: false, defenderOwned: 0, attackerOwned: 0);

        Assert.False(sizing.Ready);
        Assert.False(sizing.SizeNow);
        Assert.False(sizing.HasAnyOwnedTroops);
    }

    [Fact]
    public void BothPopulated_WithTroops_SizesJointly()
    {
        // A non-host: own defender side owns troops, enemy attacker side is an empty (but populated) reserve.
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 7, attackerOwned: 0);

        Assert.True(sizing.Ready);
        Assert.True(sizing.SizeNow);
        Assert.True(sizing.HasAnyOwnedTroops);
    }

    [Fact]
    public void EnemyOnlyReserve_DoesNotOpenDeploymentWithoutLocalPlayerOrigin()
    {
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null);
        attacker.SetReserve(Array.Empty<PartyReserve>());
        defender.SetReserve(new[]
        {
            new PartyReserve("enemy-party", 0, new[]
            {
                new TroopReserveEntry(1, "looter", formationClass: 0),
            }),
        });

        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 1, attackerOwned: 0);

        Assert.True(sizing.SizeNow);
        Assert.False(CoopBattleMissionSpawnHandler.HasLocalPlayerOrigin(
            BattleSideEnum.Attacker, "player-party", defender, attacker));
    }

    [Fact]
    public void LocalPartyHeroOrigin_AllowsDeploymentSizing()
    {
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null);
        attacker.SetReserve(new[]
        {
            new PartyReserve("player-party", 0, new[]
            {
                new TroopReserveEntry(1, "main-hero", formationClass: 0),
            }),
        });
        defender.SetReserve(Array.Empty<PartyReserve>());

        Assert.True(CoopBattleMissionSpawnHandler.HasLocalPlayerOrigin(
            BattleSideEnum.Attacker, "player-party", defender, attacker));
    }

    [Fact]
    public void LocalPartyWithoutHero_AllowsLeaderlessDeployment()
    {
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null);
        attacker.SetReserve(new[]
        {
            new PartyReserve("player-party", suppliedCount: 0, new[]
            {
                new TroopReserveEntry(1, "recruit", formationClass: 0),
            }),
        });
        defender.SetReserve(Array.Empty<PartyReserve>());

        Assert.True(CoopBattleMissionSpawnHandler.HasLocalPlayerOrigin(
            BattleSideEnum.Attacker, "player-party", defender, attacker));
    }

    [Fact]
    public void ExhaustedLocalParty_DoesNotOpenDeployment()
    {
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null);
        attacker.SetReserve(new[]
        {
            new PartyReserve("player-party", suppliedCount: 1, new[]
            {
                new TroopReserveEntry(1, "main-hero", formationClass: 0),
            }),
        });
        defender.SetReserve(Array.Empty<PartyReserve>());

        Assert.False(CoopBattleMissionSpawnHandler.HasLocalPlayerOrigin(
            BattleSideEnum.Attacker, "player-party", defender, attacker));
    }

    [Fact]
    public void BothPopulated_BothEmpty_ReadyButDoesNotRunInit()
    {
        // Defensive: both sides owning nothing must not hand Init a 0/0 total (which would divide by zero).
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 0, attackerOwned: 0);

        Assert.True(sizing.Ready);
        Assert.False(sizing.SizeNow);
        Assert.False(sizing.HasAnyOwnedTroops);
    }

    [Fact]
    public void EndConditionHold_OneSidedFallback_ReleasesOnlyAfterOtherSideFieldsAndDeploymentActivates()
    {
        Assert.False(CoopBattleController.ShouldReleaseEndConditionHold(
            deploymentActivated: false,
            attackerFielded: true,
            defenderFielded: false,
            attackerMissingReserveAccepted: false,
            defenderMissingReserveAccepted: true));

        Assert.False(CoopBattleController.ShouldReleaseEndConditionHold(
            deploymentActivated: true,
            attackerFielded: false,
            defenderFielded: false,
            attackerMissingReserveAccepted: false,
            defenderMissingReserveAccepted: true));

        Assert.True(CoopBattleController.ShouldReleaseEndConditionHold(
            deploymentActivated: true,
            attackerFielded: true,
            defenderFielded: false,
            attackerMissingReserveAccepted: false,
            defenderMissingReserveAccepted: true));
    }

    [Fact]
    public void EndConditionHold_BothMissingFallback_DoesNotReleaseEmptyBattle()
    {
        Assert.False(CoopBattleController.ShouldReleaseEndConditionHold(
            deploymentActivated: true,
            attackerFielded: false,
            defenderFielded: false,
            attackerMissingReserveAccepted: true,
            defenderMissingReserveAccepted: true));
    }

    [Fact]
    public void MigrationRecoveryTargets_LargeArmyReserves_StayWithinJointBattleSize()
    {
        var targets = ReinforcementFielder.RecoveryTargets.Calculate(
            defenderTotal: 1154,
            attackerTotal: 1336,
            battleSize: 1000,
            maximumSideRatio: 0.75f,
            defenderAdvantageFactor: 1f);

        Assert.Equal(464, targets.Defenders);
        Assert.Equal(536, targets.Attackers);
        Assert.Equal(1000, targets.Defenders + targets.Attackers);
    }

    [Fact]
    public void MigrationRecoveryTargets_OneSidedReserve_UsesAvailableBattleCapacity()
    {
        var targets = ReinforcementFielder.RecoveryTargets.Calculate(
            defenderTotal: 1000,
            attackerTotal: 0,
            battleSize: 500,
            maximumSideRatio: 0.75f,
            defenderAdvantageFactor: 1f);

        Assert.Equal(500, targets.Defenders);
        Assert.Equal(0, targets.Attackers);
    }

    [Fact]
    public void MigrationRecoveryTargets_SmallBattle_DoesNotInventTroops()
    {
        var targets = ReinforcementFielder.RecoveryTargets.Calculate(
            defenderTotal: 40,
            attackerTotal: 60,
            battleSize: 1000,
            maximumSideRatio: 0.75f,
            defenderAdvantageFactor: 1f);

        Assert.Equal(40, targets.Defenders);
        Assert.Equal(60, targets.Attackers);
    }

    [Theory]
    [InlineData(129, 0, 129)]
    [InlineData(129, 120, 9)]
    [InlineData(129, 129, 0)]
    [InlineData(129, 140, 0)]
    public void MigrationRecoveryParty_ReconcilesActiveRosterAgainstAgentsThatActuallyArrived(
        int activeRoster,
        int liveAgents,
        int expectedMissing)
    {
        var missing = ReinforcementFielder.CalculateMissingByCharacter(
            new Dictionary<string, int> { ["imperial_recruit"] = activeRoster },
            new Dictionary<string, int> { ["imperial_recruit"] = liveAgents });

        Assert.Equal(expectedMissing, missing.TryGetValue("imperial_recruit", out var count) ? count : 0);
    }

    [Fact]
    public void EndConditionHold_MustObserveFieldedSidesBeforeTerminalReplayDepletesOne()
    {
        Assert.True(CoopBattleController.ShouldReleaseEndConditionHold(
            deploymentActivated: true,
            attackerFielded: true,
            defenderFielded: true,
            attackerMissingReserveAccepted: false,
            defenderMissingReserveAccepted: false));

        Assert.False(CoopBattleController.ShouldReleaseEndConditionHold(
            deploymentActivated: true,
            attackerFielded: true,
            defenderFielded: false,
            attackerMissingReserveAccepted: false,
            defenderMissingReserveAccepted: false));
    }
}
