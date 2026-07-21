using Missions.Battles;
using System.Collections.Generic;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Game-independent tests for <see cref="CoopBattleMissionSpawnHandler.SideSizing"/>: sizing waits until both
/// reserves land (the joint cap needs both totals) and only runs Init on a positive combined total.
/// </summary>
public class CoopBattleMissionSpawnHandlerSizingTests
{
    [Fact]
    public void InitialPhaseRebase_PreservesTheWaveDepthTruncatedByNativeInit()
    {
        var sizing = CoopBattleMissionSpawnHandler.InitialPhaseSizing.Calculate(
            representedTotalTroops: 7,
            representedInitialTroops: 2,
            spawnedTroops: 0,
            reservedTroops: 0,
            unsuppliedTroops: 10,
            unsuppliedInitialTroops: 2);

        Assert.Equal(7, sizing.TotalTroops);
        Assert.Equal(2, sizing.InitialTroops);
        Assert.Equal(5, sizing.RemainingTroops);
    }

    [Fact]
    public void InitialPhaseRebase_ShrinksBelowRepresentedDepthWhenScopeCannotFillIt()
    {
        var sizing = CoopBattleMissionSpawnHandler.InitialPhaseSizing.Calculate(
            representedTotalTroops: 7,
            representedInitialTroops: 2,
            spawnedTroops: 0,
            reservedTroops: 0,
            unsuppliedTroops: 1,
            unsuppliedInitialTroops: 1);

        Assert.Equal(1, sizing.TotalTroops);
        Assert.Equal(1, sizing.InitialTroops);
        Assert.Equal(0, sizing.RemainingTroops);
    }

    [Fact]
    public void OneSideNotPopulated_NotReady_HoldsBothSides()
    {
        // Own side already has its reserve but the enemy side's (empty) reserve is still in flight: not ready,
        // so both sides stay held at zero until the second reserve lands.
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: false, defenderOwned: 7, attackerOwned: 0,
            defenderInitial: 7, attackerInitial: 0);

        Assert.False(sizing.Ready);
        Assert.False(sizing.SizeNow);
        Assert.True(sizing.HasAnyOwnedTroops);
    }

    [Fact]
    public void NeitherPopulated_NotReady()
    {
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: false, attackerPopulated: false, defenderOwned: 0, attackerOwned: 0,
            defenderInitial: 0, attackerInitial: 0);

        Assert.False(sizing.Ready);
        Assert.False(sizing.SizeNow);
        Assert.False(sizing.HasAnyOwnedTroops);
    }

    [Fact]
    public void BothPopulated_WithTroops_SizesJointly()
    {
        // A non-host: own defender side owns troops, enemy attacker side is an empty (but populated) reserve.
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 7, attackerOwned: 0,
            defenderInitial: 4, attackerInitial: 0);

        Assert.True(sizing.Ready);
        Assert.True(sizing.SizeNow);
        Assert.True(sizing.HasAnyOwnedTroops);
        Assert.Equal(7, sizing.DefenderOwned);
        Assert.Equal(4, sizing.DefenderInitial);
    }

    [Fact]
    public void BothPopulated_BothEmpty_ReadyButDoesNotRunInit()
    {
        // Defensive: both sides owning nothing must not hand Init a 0/0 total (which would divide by zero).
        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 0, attackerOwned: 0,
            defenderInitial: 0, attackerInitial: 0);

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
    public void MigrationRecoverySlots_SubtractActiveTroopsFromAuthorityEntitlements()
    {
        var slots = ReinforcementFielder.RecoverySlots.Calculate(
            defenderEntitlement: 120,
            attackerEntitlement: 80,
            activeDefenders: 70,
            activeAttackers: 50,
            battleSize: 200);

        Assert.Equal(50, slots.Defenders);
        Assert.Equal(30, slots.Attackers);
    }

    [Fact]
    public void MigrationRecoverySlots_ActiveAtOrAboveEntitlementClampsToZero()
    {
        var slots = ReinforcementFielder.RecoverySlots.Calculate(
            defenderEntitlement: 120,
            attackerEntitlement: 80,
            activeDefenders: 120,
            activeAttackers: 95,
            battleSize: 200);

        Assert.Equal(0, slots.Defenders);
        Assert.Equal(0, slots.Attackers);
    }

    [Fact]
    public void MigrationRecoverySlots_DoesNotBorrowUnusedCapacityAcrossSides()
    {
        var slots = ReinforcementFielder.RecoverySlots.Calculate(
            defenderEntitlement: 100,
            attackerEntitlement: 50,
            activeDefenders: 0,
            activeAttackers: 50,
            battleSize: 150);

        Assert.Equal(100, slots.Defenders);
        Assert.Equal(0, slots.Attackers);
        Assert.Equal(100, slots.Defenders + slots.Attackers);
    }

    [Fact]
    public void MigrationRecoverySlots_ActivePlusRecoveredNeverExceedsBattleSize()
    {
        var slots = ReinforcementFielder.RecoverySlots.Calculate(
            defenderEntitlement: 700,
            attackerEntitlement: 700,
            activeDefenders: 400,
            activeAttackers: 400,
            battleSize: 1000);

        Assert.Equal(100, slots.Defenders);
        Assert.Equal(100, slots.Attackers);
        Assert.Equal(1000, 400 + 400 + slots.Defenders + slots.Attackers);
    }

    [Fact]
    public void MigrationRecoverySlots_ExistingSideOverflowConsumesJointCapacity()
    {
        var slots = ReinforcementFielder.RecoverySlots.Calculate(
            defenderEntitlement: 500,
            attackerEntitlement: 500,
            activeDefenders: 600,
            activeAttackers: 0,
            battleSize: 1000);

        Assert.Equal(0, slots.Defenders);
        Assert.Equal(400, slots.Attackers);
        Assert.Equal(1000, 600 + slots.Attackers);
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
