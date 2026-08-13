using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
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

    // A side divided between players is SIZED from the whole side's strength (so the engine's battle-size split
    // stays proportional) but can only ever be SUPPLIED with this client's share of it. CheckDeployment treats
    // the sized number as a target this client must fill and SKIPS THE WHOLE SIDE - plan-making included - until
    // it does, so a target the supplier will never return leaves the side unplanned and unspawned forever.
    [Fact]
    public void SpawnTargetIsWhatTheSupplierWouldActuallyReturn()
    {
        // The live wedge: a 163-troop wave on a side of 955, of which this client owns 382. Asked for 163 the
        // supplier hands back its share, 65 - so 163 is unreachable and 65 is the honest target.
        Assert.Equal(65, CoopBattleMissionSpawnHandler.ReachableSpawnNumber(163, 65));

        // Sole owner of the side: it supplies the whole wave, so the engine's number stands.
        Assert.Equal(163, CoopBattleMissionSpawnHandler.ReachableSpawnNumber(163, 163));

        // Owns nothing on that side (its troops arrive as replicated puppets): spawn nothing locally.
        Assert.Equal(0, CoopBattleMissionSpawnHandler.ReachableSpawnNumber(163, 0));

        // A share can never exceed what the side actually needs.
        Assert.Equal(163, CoopBattleMissionSpawnHandler.ReachableSpawnNumber(163, 400));
    }

    [Fact]
    public void InitialAllocation_ReservesEveryPlayerWithoutChangingBattleSize()
    {
        CoopBattleMissionSpawnHandler.AdjustInitialAllocations(
            defenderInitial: 1,
            attackerInitial: 599,
            defenderTotal: 100,
            attackerTotal: 900,
            defenderPlayers: 3,
            attackerPlayers: 2,
            out var defenders,
            out var attackers);

        Assert.Equal(3, defenders);
        Assert.Equal(597, attackers);
        Assert.Equal(600, defenders + attackers);
    }

    [Fact]
    public void PhaseShare_PreservesTotalInitialRemainingInvariantForTinyPlayerParty()
    {
        CoopBattleMissionSpawnHandler.AdjustPhaseToOwnedShare(
            sideTotal: 100,
            sideInitial: 40,
            ownedTotal: 1,
            ownedInitial: 1,
            out var total,
            out var initial,
            out var remaining);

        Assert.Equal(1, total);
        Assert.Equal(1, initial);
        Assert.Equal(0, remaining);
        Assert.Equal(total, initial + remaining);
    }

    [Fact]
    public void RefreshedLifetimeQuota_PreservesCommittedTroopsAndChangesOnlyUnspentQuota()
    {
        var phase = new MissionSpawnPhase
        {
            TotalSpawnNumber = 100,
            InitialSpawnedNumber = 40,
            InitialSpawnNumber = 0,
            RemainingSpawnNumber = 50,
        };

        CoopBattleMissionSpawnHandler.ReconcilePhaseLifetimeQuota(
            phase, refreshedOwnedTarget: 130, supplied: 55, reserved: 5);

        Assert.Equal(130, phase.TotalSpawnNumber);
        Assert.Equal(80, phase.RemainingSpawnNumber);
        Assert.Equal(40, phase.InitialSpawnedNumber);
        Assert.Equal(0, phase.InitialSpawnNumber);
    }

    [Fact]
    public void RefreshedLifetimeQuota_DoesNotReplayRecoveryClaimedTroops()
    {
        var phase = new MissionSpawnPhase
        {
            TotalSpawnNumber = 100,
            InitialSpawnedNumber = 40,
            RemainingSpawnNumber = 50,
        };

        CoopBattleMissionSpawnHandler.ReconcilePhaseLifetimeQuota(
            phase, refreshedOwnedTarget: 130, supplied: 70, reserved: 5);

        Assert.Equal(130, phase.TotalSpawnNumber);
        Assert.Equal(65, phase.RemainingSpawnNumber);
    }

    [Theory]
    [InlineData(1000, 600, 0.5f, 3, 1000)]
    [InlineData(5000, 600, 0.5f, 3, 1500)]
    [InlineData(5000, 600, 0.5f, 0, 5000)]
    public void RefreshedLifetimeTarget_MatchesNativeWaveLimit(
        int sideTotal, int initialTarget, float wavePercentage, int maximumWaves, int expected)
    {
        Assert.Equal(expected, CoopBattleMissionSpawnHandler.CalculateLifetimeTarget(
            sideTotal, initialTarget, wavePercentage, maximumWaves));
    }

    [Fact]
    public void ConsecutiveRefreshes_DoNotReconcileAMixedSidePair()
    {
        Assert.Equal(0, CoopBattleMissionSpawnHandler.MatchingAllocationRevision(2, 1));
        Assert.Equal(2, CoopBattleMissionSpawnHandler.MatchingAllocationRevision(2, 2));
        Assert.Equal(0, CoopBattleMissionSpawnHandler.MatchingAllocationRevision(3, 2));
        Assert.Equal(3, CoopBattleMissionSpawnHandler.MatchingAllocationRevision(3, 3));
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
        var agentBudget = new BattleAgentBudget();
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null, agentBudget);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null, agentBudget);
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
        var agentBudget = new BattleAgentBudget();
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null, agentBudget);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null, agentBudget);
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
        var agentBudget = new BattleAgentBudget();
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null, agentBudget);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null, agentBudget);
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
    public void ServerAssignedPlayerPartyId_IsUsedAsTheLocalOrigin()
    {
        var agentBudget = new BattleAgentBudget();
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null, agentBudget);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null, agentBudget);
        attacker.SetReserve(new[]
        {
            new PartyReserve("MapEventParty_Created_34", 0, new[]
            {
                new TroopReserveEntry(1, "main-hero", formationClass: 0),
            }, isReceiverPlayerParty: true),
        });
        defender.SetReserve(Array.Empty<PartyReserve>());

        Assert.Equal("MapEventParty_Created_34", attacker.PlayerPartyId);
        Assert.True(CoopBattleMissionSpawnHandler.HasLocalPlayerOrigin(
            BattleSideEnum.Attacker, attacker.PlayerPartyId, defender, attacker));

        attacker.SetReserve(Array.Empty<PartyReserve>());
        Assert.Null(attacker.PlayerPartyId);
    }

    [Fact]
    public void ExhaustedLocalParty_DoesNotOpenDeployment()
    {
        var agentBudget = new BattleAgentBudget();
        var attacker = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null, agentBudget);
        var defender = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null, agentBudget);
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

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void EndConditionHold_HideoutControllersRetainNativeOwnership(
        bool hasHideoutMissionController,
        bool hasHideoutAmbushMissionController,
        bool expected)
    {
        Assert.Equal(expected, CoopBattleController.ShouldManageBattleEndLogic(
            hasHideoutMissionController,
            hasHideoutAmbushMissionController));
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

    [Fact]
    public void LatePartyRecovery_DoesNotExceedOwnerOrWholeSideTarget()
    {
        Assert.Equal(5, ReinforcementFielder.AvailableRecoverySlots(
            ownedTarget: 100, activeOwned: 95, sideTarget: 600, activeSide: 550));
        Assert.Equal(2, ReinforcementFielder.AvailableRecoverySlots(
            ownedTarget: 100, activeOwned: 90, sideTarget: 600, activeSide: 598));
        Assert.Equal(0, ReinforcementFielder.AvailableRecoverySlots(
            ownedTarget: 100, activeOwned: 100, sideTarget: 600, activeSide: 550));
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
