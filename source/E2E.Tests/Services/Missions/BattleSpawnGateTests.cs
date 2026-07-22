using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests for <see cref="BattleSpawnGate"/> — the static flag the (GameInterface) battle-spawn patches read
/// to know a coop field battle is the active mission. It holds no host/ownership state (which client fields which
/// troops is decided server-side by the reserve assignment), so only the active/map-event lifecycle is covered.
/// </summary>
[Collection(BattleSpawnGateTestCollection.Name)]
public class BattleSpawnGateTests : IDisposable
{
    public BattleSpawnGateTests()
    {
        BattleSpawnGate.ResetPrioritySpawnSnapshot("mapEvent-1");
        BattleSpawnGate.ResetPrioritySpawnSnapshot("mapEvent-2");
    }

    public void Dispose() => BattleSpawnGate.EndBattle();

    [Fact]
    public void NoActiveBattle_GateIsInactive()
    {
        BattleSpawnGate.EndBattle();

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
        Assert.Equal(0, BattleSpawnGate.BattleSize);
    }

    [Fact]
    public void BeginBattle_MarksActive_WithTheMapEvent()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", 725);

        Assert.True(BattleSpawnGate.IsCoopBattleActive);
        Assert.Equal("mapEvent-1", BattleSpawnGate.ActiveMapEventId);
        Assert.Equal(725, BattleSpawnGate.BattleSize);
    }

    [Fact]
    public void PrioritySpawnQueuedBeforeMissionEntry_SurvivesBeginBattle()
    {
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");

        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);

        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
    }

    [Fact]
    public void PendingPrioritySpawn_PreventsSideDepletionUntilTheWaitClears()
    {
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.False(CoopBattleDepletionPatch.DetermineSideDepleted(
            hadAgents: true,
            side: BattleSideEnum.Defender));

        Assert.True(BattleSpawnGate.CancelUnassignedPrioritySpawn(
            "mapEvent-1",
            "waiting-party"));
        Assert.True(CoopBattleDepletionPatch.DetermineSideDepleted(
            hadAgents: true,
            side: BattleSideEnum.Defender));
    }

    [Fact]
    public void PriorityAssignment_ReplacesStaleTransferForTheSameWaitingParty()
    {
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 2, "waiting-party", "donor-2");
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-1");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        var assignments = BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1");

        var assignment = Assert.Single(assignments);
        Assert.Equal(1, assignment.TransferId);
        Assert.Equal("waiting-party", assignment.WaitingPartyId);
        Assert.Equal("donor-1", assignment.DonorPartyId);

        Assert.True(BattleSpawnGate.CompletePrioritySpawn("mapEvent-1", "waiting-party"));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        Assert.True(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
    }

    [Fact]
    public void ConsumedAssignment_RemainsPendingUntilThePuppetOrDepartureCompletesIt()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));

        Assert.True(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
    }

    [Fact]
    public void SettledAssignment_ClearsRetainedTransferWithoutReopeningTheGate()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        Assert.True(BattleSpawnGate.CompletePrioritySpawn(
            "mapEvent-1", "waiting-party"));
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.True(BattleSpawnGate.SettlePrioritySpawn(
            "mapEvent-1", 1, "waiting-party"));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.False(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "waiting-party"));

        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
    }

    [Fact]
    public void PuppetBeforeAssignment_RetainsLateAssignmentWithoutReopeningTheGate()
    {
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.True(BattleSpawnGate.CompletePrioritySpawn(
            "mapEvent-1", "waiting-party"));
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");

        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));

        Assert.True(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
    }

    [Fact]
    public void AssignmentBeforePuppet_RemainsAvailableForConsumedAcknowledgement()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.True(BattleSpawnGate.CompletePrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.True(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
    }

    [Fact]
    public void DepartedUnconsumedAssignment_WaitsForServerCancellation()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.False(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));

        Assert.True(BattleSpawnGate.CancelPrioritySpawnAssignment("mapEvent-1", 1));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
    }

    [Fact]
    public void SameTransferReassignment_ClearsThePreviousWaitersConsumedState()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "departed-waiter", "donor-party");
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "departed-waiter"));

        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "next-waiter", "donor-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.False(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "departed-waiter"));
        Assert.False(BattleSpawnGate.ClearConsumedPrioritySpawn(
            "mapEvent-1", "next-waiter"));
        var assignment = Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.Equal("next-waiter", assignment.WaitingPartyId);
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
    }

    [Fact]
    public void ReconnectReset_PreservesPuppetRegistrationAndDoesNotReopenGate()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "old-donor");
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        Assert.True(BattleSpawnGate.CompletePrioritySpawn(
            "mapEvent-1", "waiting-party"));

        BattleSpawnGate.ResetAndQueuePrioritySpawn("mapEvent-1", "waiting-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.False(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);

        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 2, "waiting-party", "new-donor");
        var assignment = Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.Equal(2, assignment.TransferId);
        Assert.Equal("new-donor", assignment.DonorPartyId);
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 2, "waiting-party"));
    }

    [Fact]
    public void EmptyEntrySnapshot_ClearsLateTransferStateFromThePreviousMission()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);
        BattleSpawnGate.EndBattle();

        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "stale-waiter");
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "stale-waiter", "stale-donor");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));

        BattleSpawnGate.ResetPrioritySpawnSnapshot("mapEvent-1");

        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
    }

    [Fact]
    public void EntrySnapshotReset_PreservesRegisteredPuppetAcrossBothAssignmentReplays()
    {
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);
        Assert.True(BattleSpawnGate.CompletePrioritySpawn(
            "mapEvent-1", "waiting-party"));

        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        Assert.True(BattleSpawnGate.MarkPrioritySpawnConsumed(
            "mapEvent-1", 1, "waiting-party"));
        BattleSpawnGate.ResetPrioritySpawnSnapshot("mapEvent-1");

        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");

        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
    }

    [Fact]
    public void PriorityAssignment_ReassignmentReplacesPreviousWaiter()
    {
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "departed-waiter", "donor-party");
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "next-waiter", "donor-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        var assignment = Assert.Single(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
        Assert.Equal("next-waiter", assignment.WaitingPartyId);
        Assert.False(BattleSpawnGate.CancelPrioritySpawn("mapEvent-1", "departed-waiter"));
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);

        Assert.True(BattleSpawnGate.CancelPrioritySpawnAssignment("mapEvent-1", 1));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
        Assert.Empty(BattleSpawnGate.GetPrioritySpawnAssignments("mapEvent-1"));
    }

    [Fact]
    public void DepartedWaiter_AssignedSlotRemainsUntilServerCancellation()
    {
        BattleSpawnGate.QueuePrioritySpawn("mapEvent-1", "waiting-party");
        BattleSpawnGate.RecordPrioritySpawnAssignment(
            "mapEvent-1", 1, "waiting-party", "donor-party");
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.False(BattleSpawnGate.CancelUnassignedPrioritySpawn(
            "mapEvent-1", "waiting-party"));
        Assert.True(BattleSpawnGate.HasPendingPrioritySpawn);

        Assert.True(BattleSpawnGate.CancelPrioritySpawnAssignment("mapEvent-1", 1));
        Assert.False(BattleSpawnGate.HasPendingPrioritySpawn);
    }

    [Fact]
    public void HasAvailableHumanAgentSlot_NoActiveMission_ReturnsFalse()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        Assert.False(BattleSpawnGate.HasAvailableHumanAgentSlot(null));
    }

    [Fact]
    public void EndBattle_ClearsActiveBattle()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);

        BattleSpawnGate.EndBattle();

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
        Assert.Equal(0, BattleSpawnGate.BattleSize);
    }

    [Fact]
    public void MissingReserveAcceptance_IsPerSide_AndClearedForNextBattle()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", 1000);
        BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Defender);

        Assert.True(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
        Assert.False(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));

        BattleSpawnGate.BeginBattle("mapEvent-2", 1000);

        Assert.False(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
        Assert.False(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));
    }

    [Fact]
    public void RoutedAttackerWeapon_IsScopedAndRestored()
    {
        var outerWeapon = new WeaponComponentData(null, WeaponClass.Arrow, default);
        var innerWeapon = new WeaponComponentData(null, WeaponClass.Bolt, default);

        Assert.Null(BattleSpawnGate.RoutedAttackerWeapon);

        BattleSpawnGate.RunWithRoutedAttackerWeapon(outerWeapon, () =>
        {
            Assert.Same(outerWeapon, BattleSpawnGate.RoutedAttackerWeapon);
            BattleSpawnGate.RunWithRoutedAttackerWeapon(innerWeapon,
                () => Assert.Same(innerWeapon, BattleSpawnGate.RoutedAttackerWeapon));
            Assert.Same(outerWeapon, BattleSpawnGate.RoutedAttackerWeapon);
        });

        Assert.Null(BattleSpawnGate.RoutedAttackerWeapon);
    }
}
