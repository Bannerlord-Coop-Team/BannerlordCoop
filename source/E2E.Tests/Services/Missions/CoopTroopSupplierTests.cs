using Common.Util;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Battles;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests for <see cref="CoopTroopSupplier"/>'s supply/pointer behaviour (independent of the game): the
/// "not populated yet" gate so deployment waits for the server's reserve; an empty (non-owned) side reporting
/// "done" rather than hanging; per-party pointers advancing as the native logic pulls troops; and resuming
/// from the server's pointer on migration. Origin creation needs the object manager, so it is exercised live.
/// </summary>
public class CoopTroopSupplierTests
{
    private static TroopReserveEntry[] Entries(int count, int seedBase = 500)
    {
        var entries = new TroopReserveEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = new TroopReserveEntry(seedBase + i, $"Char_{i}", formationClass: 0);
        return entries;
    }

    private static PartyReserve Party(string id, int count, int supplied = 0, int seedBase = 500, int? initialSpawnCount = null)
        => new PartyReserve(id, supplied, Entries(count, seedBase), initialSpawnCount ?? count);

    private static int SuppliedFor(CoopTroopSupplier supplier, string partyId)
        => supplier.GetSuppliedByParty().First(p => p.partyId == partyId).supplied;

    private static CoopBattleMissionSpawnHandler.SideSizing ReadSizing(
        CoopTroopSupplier defender,
        CoopTroopSupplier attacker)
    {
        CoopTroopSupplier.GetSizingSnapshots(defender, attacker, out var defenderSnapshot, out var attackerSnapshot);
        return new CoopBattleMissionSpawnHandler.SideSizing(
            defenderSnapshot.IsPopulated,
            attackerSnapshot.IsPopulated,
            defenderSnapshot.TotalTroops,
            attackerSnapshot.TotalTroops,
            defenderSnapshot.InitialTroops,
            attackerSnapshot.InitialTroops,
            defenderSnapshot.GrantGeneration,
            attackerSnapshot.GrantGeneration,
            defenderSnapshot.CompletesInitialSizing,
            attackerSnapshot.CompletesInitialSizing);
    }

    private static CoopTroopSupplier CreateResolvableSupplier(string mapEventId, string partyId, int count)
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        for (int index = 0; index < count; index++)
            objectManager.AddExisting($"Char_{index}", ObjectHelper.SkipConstructor<CharacterObject>());

        var supplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, objectManager);
        supplier.SetReserve(new[] { Party(partyId, count) });
        return supplier;
    }

    [Fact]
    public void BeforePopulated_ReportsTroopsStillComing_AndSuppliesNone()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);

        Assert.True(supplier.AnyTroopRemainsToBeSupplied); // not populated -> deployment must wait
        Assert.Empty(supplier.SupplyTroops(5));
    }

    [Fact]
    public void EmptyReserve_MarksPopulated_AndReportsDone()
    {
        // A side this client owns nothing on still gets an (empty) reserve, so deployment completes.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(Array.Empty<PartyReserve>());

        Assert.False(supplier.AnyTroopRemainsToBeSupplied);
        Assert.Equal(0, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void SetReserve_ThenSupply_AdvancesPerPartyPointer()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 10) });

        Assert.Equal(10, supplier.NumTroopsNotSupplied);

        supplier.SupplyTroops(4);
        Assert.Equal(6, supplier.NumTroopsNotSupplied);
        Assert.Equal(4, SuppliedFor(supplier, "A"));

        supplier.SupplyOneTroop();
        Assert.Equal(5, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void InitialTroops_UsesLeasesWhileTotalTroopsKeepsFullReserve()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[]
        {
            Party("A", 100, initialSpawnCount: 25),
            Party("B", 60, seedBase: 700, initialSpawnCount: 15),
        });

        Assert.Equal(40, supplier.InitialTroops);
        Assert.Equal(160, supplier.TotalTroops);
        Assert.Equal(160, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void InitialSupply_HonorsEachCapturedPartyLeaseBeforeNormalWaves()
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        for (int index = 0; index < 5; index++)
            objectManager.AddExisting($"Char_{index}", ObjectHelper.SkipConstructor<CharacterObject>());
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, objectManager);
        supplier.SetReserve(new[]
        {
            Party("AI", 4, seedBase: 100, initialSpawnCount: 1),
            Party("player", 1, seedBase: 200, initialSpawnCount: 1),
        });
        var sizing = supplier.GetSizingSnapshot();

        supplier.BeginInitialSupply(sizing.PartyCapacities);
        var initial = supplier.SupplyTroops(sizing.InitialTroops + 1)
            .Cast<CoopAgentOrigin>()
            .Select(origin => origin.UniqueSeed)
            .ToArray();

        Assert.Equal(new[] { 100, 200 }, initial);
        Assert.Equal(1, SuppliedFor(supplier, "AI"));
        Assert.Equal(1, SuppliedFor(supplier, "player"));
        var wave = Assert.IsType<CoopAgentOrigin>(supplier.SupplyOneTroop());
        Assert.Equal(101, wave.UniqueSeed);
        Assert.Equal(2, SuppliedFor(supplier, "AI"));
        Assert.Equal(1, SuppliedFor(supplier, "player"));
    }

    [Fact]
    public void InitialSupply_ScopeShrinkRebasesCapturedPartiesWithoutSpillingIntoAWave()
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        for (int index = 0; index < 4; index++)
            objectManager.AddExisting($"Char_{index}", ObjectHelper.SkipConstructor<CharacterObject>());
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, objectManager);
        supplier.SetReserve(new[]
        {
            Party("AI", 4, seedBase: 100, initialSpawnCount: 1),
            Party("player", 1, seedBase: 200, initialSpawnCount: 1),
        });
        var sizing = supplier.GetSizingSnapshot();
        supplier.BeginInitialSupply(sizing.PartyCapacities);

        supplier.SetReserve(new[] { Party("AI", 4, seedBase: 100, initialSpawnCount: 1) });
        var rebased = supplier.GetInitialPhaseSnapshot();

        Assert.True(rebased.IsCaptured);
        Assert.Equal(4, rebased.RemainingTroops);
        Assert.Equal(1, rebased.RemainingInitialTroops);
        Assert.True(supplier.CommitInitialPhaseRebase(rebased.ReserveRevision));
        var initial = supplier.SupplyTroops(2).Cast<CoopAgentOrigin>().ToArray();
        Assert.Equal(100, Assert.Single(initial).UniqueSeed);
        Assert.Equal(1, SuppliedFor(supplier, "AI"));

        var wave = Assert.IsType<CoopAgentOrigin>(supplier.SupplyOneTroop());
        Assert.Equal(101, wave.UniqueSeed);
    }

    [Fact]
    public void InitialTroops_ClampsInvalidLeaseToPartyCapacity()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[]
        {
            Party("negative", 10, initialSpawnCount: -2),
            Party("oversized", 20, seedBase: 700, initialSpawnCount: 50),
        });

        Assert.Equal(20, supplier.InitialTroops);
    }

    [Fact]
    public void SetReserve_LatestSnapshotReplacesInitialEntitlement()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 100, initialSpawnCount: 25) });

        supplier.SetReserve(new[] { Party("A", 100, initialSpawnCount: 0) });

        Assert.Equal(0, supplier.InitialTroops);
    }

    [Fact]
    public void SupplyingPastEnd_StopsAndReportsExhausted()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 3) });

        supplier.SupplyTroops(99);

        Assert.Equal(0, supplier.NumTroopsNotSupplied);
        Assert.False(supplier.AnyTroopRemainsToBeSupplied);
        Assert.Null(supplier.SupplyOneTroop());
        Assert.Equal(3, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void Supply_SpansMultipleParties_InOrder()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 2, seedBase: 100), Party("B", 3, seedBase: 200) });

        Assert.Equal(5, supplier.NumTroopsNotSupplied);

        supplier.SupplyTroops(4); // 2 from A, then 2 from B
        Assert.Equal(2, SuppliedFor(supplier, "A"));
        Assert.Equal(2, SuppliedFor(supplier, "B"));
    }

    [Fact]
    public void StaleResend_DoesNotRewind_FurtherAlongPointer()
    {
        // Migration/race: we've already supplied 5, but a resend carries a STALE pointer (3) because our last
        // progress report hasn't reached the server's ledger yet. Re-applying it must NOT rewind to 3 (which
        // would re-spawn troops 4 and 5, already on the field, with duplicate seeds) — the pointer is monotonic.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 10) });
        supplier.SupplyTroops(5);
        Assert.Equal(5, SuppliedFor(supplier, "A"));

        supplier.SetReserve(new[] { new PartyReserve("A", 3, Entries(10), 10) });

        Assert.Equal(5, SuppliedFor(supplier, "A"));
        Assert.Equal(5, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void Resend_WithHigherPointer_AdvancesToServer()
    {
        // The normal migration resume: a party the server is further along on (or one we hadn't supplied
        // locally) takes the server's higher pointer.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { Party("A", 10) });
        supplier.SupplyTroops(2);

        supplier.SetReserve(new[] { new PartyReserve("A", 6, Entries(10), 10) });

        Assert.Equal(6, SuppliedFor(supplier, "A"));
    }

    [Fact]
    [Trait("Requirement", "BR-033")]
    public void Refeed_WithoutAParty_DropsItsReserve()
    {
        // The reconnect shrink-refresh relies on SetReserve's REPLACE semantics: when a dropped owner
        // returns, the server re-feeds the holder its CURRENT owned set WITHOUT the returned party, and the
        // supplier must stop holding that party's reserve entirely (otherwise two suppliers would field the
        // same troops). Parties that remain keep their monotonic pointer.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { Party("returned", 4, seedBase: 100), Party("kept", 3, seedBase: 200) });
        supplier.SupplyTroops(1); // pointer advanced on "returned" before the shrink lands

        supplier.SetReserve(new[] { Party("kept", 3, seedBase: 200) });

        var held = supplier.GetSuppliedByParty();
        var only = Assert.Single(held);
        Assert.Equal("kept", only.partyId);
        Assert.Equal(3, supplier.TotalTroops);
        Assert.Equal(3, supplier.NumTroopsNotSupplied); // nothing of "returned" remains to be supplied here
    }

    [Fact]
    public void SetReserve_WithSuppliedPointer_ResumesMidway()
    {
        // Migration: a new owner is handed the full list at the server's pointer and continues from there.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { new PartyReserve("A", 7, Entries(10), 10) });

        Assert.Equal(3, supplier.NumTroopsNotSupplied);
        Assert.Equal(7, SuppliedFor(supplier, "A"));

        supplier.SupplyTroops(2);
        Assert.Equal(9, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void SetReserve_IncrementsRevision_ForEachAuthoritativeSnapshot()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);

        Assert.Equal(0, supplier.ReserveRevision);

        supplier.SetReserve(new[] { Party("A", 2) });
        supplier.SetReserve(new[] { Party("A", 2) });

        Assert.Equal(2, supplier.ReserveRevision);
    }

    [Fact]
    public void SizingSnapshot_ReturnsTotalsAndEntitlementFromOneRevision()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);

        var before = supplier.GetSizingSnapshot();
        Assert.False(before.IsPopulated);
        Assert.Equal(0, before.TotalTroops);
        Assert.Equal(0, before.InitialTroops);
        Assert.Equal(0, before.ReserveRevision);
        Assert.Equal(0, before.GrantGeneration);
        Assert.False(before.CompletesInitialSizing);

        supplier.SetReserve(new[]
        {
            Party("A", 20, initialSpawnCount: 7),
            Party("B", 5, seedBase: 700, initialSpawnCount: 2),
        });

        var after = supplier.GetSizingSnapshot();
        Assert.True(after.IsPopulated);
        Assert.Equal(25, after.TotalTroops);
        Assert.Equal(9, after.InitialTroops);
        Assert.Equal(1, after.ReserveRevision);
        Assert.Equal(0, after.GrantGeneration);
        Assert.True(after.CompletesInitialSizing);
    }

    [Fact]
    public void SizingSnapshot_NonzeroPointerUsesOnlyTheUnsuppliedInitialTranche()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { Party("A", 10, supplied: 2, initialSpawnCount: 5) });

        var snapshot = supplier.GetSizingSnapshot();

        Assert.Equal(8, snapshot.TotalTroops);
        Assert.Equal(3, snapshot.InitialTroops);
        var party = Assert.Single(snapshot.PartyCapacities);
        Assert.Equal(10, party.TotalTroops);
        Assert.Equal(3, party.InitialSpawnCount);
    }

    [Fact]
    public void AdvanceResolvedPrefix_ConsumesOnlyTheContiguousResolvedTail()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { Party("A", 4) });

        int advanced = supplier.AdvanceResolvedPrefix("A", new HashSet<int> { 500, 501, 503 });

        Assert.Equal(2, advanced);
        Assert.Equal(2, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void DepartedSeeds_RemainMonotonicAcrossAStaleReserveRefresh()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[]
        {
            new PartyReserve("A", 0, Entries(3), 1, departedSeeds: new[] { 501 }),
        });

        supplier.SetReserve(new[] { Party("A", 3, initialSpawnCount: 1) });

        Assert.True(supplier.WasDeparted(501));
    }

    [Fact]
    public void DepartedSeeds_AreExcludedFromSizingAndNativeSupply()
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        for (int index = 0; index < 5; index++)
            objectManager.AddExisting($"Char_{index}", ObjectHelper.SkipConstructor<CharacterObject>());
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, objectManager);
        supplier.SetReserve(new[]
        {
            new PartyReserve("A", 0, Entries(5), 3, departedSeeds: new[] { 500, 504 }),
        });

        var sizing = supplier.GetSizingSnapshot();
        supplier.BeginInitialSupply(sizing.PartyCapacities);
        var initial = supplier.SupplyTroops(sizing.InitialTroops)
            .Cast<CoopAgentOrigin>()
            .Select(origin => origin.UniqueSeed)
            .ToArray();
        var wave = Assert.IsType<CoopAgentOrigin>(supplier.SupplyOneTroop());

        Assert.Equal(3, sizing.TotalTroops);
        Assert.Equal(2, sizing.InitialTroops);
        Assert.Equal(new[] { 501, 502 }, initial);
        Assert.Equal(503, wave.UniqueSeed);
        Assert.Equal(0, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void PairSizingSnapshot_WaitsForOneCompleteMatchingGrant()
    {
        var defender = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        var attacker = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);

        defender.SetReserve(
            new[] { Party("defender-entry", 4, initialSpawnCount: 2) },
            grantGeneration: 41,
            completesInitialSizing: false);
        attacker.SetReserve(
            new[] { Party("attacker-entry", 3, initialSpawnCount: 1) },
            grantGeneration: 41,
            completesInitialSizing: false);

        Assert.False(ReadSizing(defender, attacker).Ready);

        attacker.SetReserve(
            new[] { Party("attacker-election", 30, initialSpawnCount: 11) },
            grantGeneration: 42,
            completesInitialSizing: true);

        var mixed = ReadSizing(defender, attacker);
        Assert.False(mixed.Ready);
        Assert.False(mixed.DefenderIncluded);
        Assert.True(mixed.AttackerIncluded);
        Assert.Equal(0, mixed.DefenderOwnedForSizing);
        Assert.Equal(30, mixed.AttackerOwnedForSizing);

        defender.SetReserve(
            new[] { Party("defender-election", 20, initialSpawnCount: 7) },
            grantGeneration: 42,
            completesInitialSizing: true);

        var complete = ReadSizing(defender, attacker);
        Assert.True(complete.Ready);
        Assert.True(complete.SizeNow);
        Assert.Equal(20, complete.DefenderOwnedForSizing);
        Assert.Equal(30, complete.AttackerOwnedForSizing);
        Assert.Equal(7, complete.DefenderInitialForSizing);
        Assert.Equal(11, complete.AttackerInitialForSizing);
    }

    [Fact]
    public void ClaimedTroop_IsNotReportedUntilCommitted()
    {
        var supplier = CreateResolvableSupplier("M1", "A", count: 3);

        Assert.True(supplier.TryClaimOneTroopFromParty("A", out var origin));
        var claimed = Assert.IsType<CoopAgentOrigin>(origin);

        Assert.Equal(0, SuppliedFor(supplier, "A"));
        Assert.True(supplier.TryGetPartyCounts("A", out _, out var internalSupplied, out _));
        Assert.Equal(1, internalSupplied);

        Assert.Equal(
            CoopTroopSupplier.ClaimedTroopUseResult.Committed,
            supplier.TryUseClaimedTroop("A", claimed.UniqueSeed, () => true));

        Assert.Equal(1, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void ClaimedTroop_RefreshPreservesClaimWithoutParkingAtEnd()
    {
        var supplier = CreateResolvableSupplier("M1", "A", count: 3);
        Assert.True(supplier.TryClaimOneTroopFromParty("A", out var origin));
        var claimed = Assert.IsType<CoopAgentOrigin>(origin);

        supplier.SetReserve(
            new[] { Party("A", 3) },
            grantGeneration: 2,
            completesInitialSizing: true);

        Assert.Equal(0, SuppliedFor(supplier, "A"));
        Assert.True(supplier.TryGetPartyCounts("A", out _, out var internalSupplied, out _));
        Assert.Equal(1, internalSupplied);
        Assert.Equal(3, supplier.NumTroopsNotSupplied);

        Assert.Equal(
            CoopTroopSupplier.ClaimedTroopUseResult.Committed,
            supplier.TryUseClaimedTroop("A", claimed.UniqueSeed, () => true));

        Assert.Equal(1, SuppliedFor(supplier, "A"));
        Assert.Equal(2, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void PendingExplicitClaim_BlocksNativeSupplyUntilCommitted()
    {
        var supplier = CreateResolvableSupplier("M1", "A", count: 3);
        Assert.True(supplier.TryClaimOneTroopFromParty("A", out var origin));
        var claimed = Assert.IsType<CoopAgentOrigin>(origin);

        Assert.Equal(
            CoopTroopSupplier.ClaimedTroopUseResult.Deferred,
            supplier.TryUseClaimedTroop("A", claimed.UniqueSeed, () => false));

        Assert.Empty(supplier.SupplyTroops(1));
        Assert.Null(supplier.SupplyOneTroop());
        Assert.False(supplier.TrySupplyOneTroopFromParty("A", out _));
        Assert.Equal(0, SuppliedFor(supplier, "A"));
        Assert.Equal(3, supplier.NumTroopsNotSupplied);
        Assert.True(supplier.AnyTroopRemainsToBeSupplied);

        Assert.Equal(
            CoopTroopSupplier.ClaimedTroopUseResult.Committed,
            supplier.TryUseClaimedTroop("A", claimed.UniqueSeed, () => true));

        var next = Assert.IsType<CoopAgentOrigin>(supplier.SupplyOneTroop());
        Assert.Equal(claimed.UniqueSeed + 1, next.UniqueSeed);
        Assert.Equal(2, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public async Task ClaimedTroop_CommitAndScopeDropAreAtomic()
    {
        var supplier = CreateResolvableSupplier("M1", "A", count: 3);
        Assert.True(supplier.TryClaimOneTroopFromParty("A", out var origin));
        var claimed = Assert.IsType<CoopAgentOrigin>(origin);
        using var useStarted = new ManualResetEventSlim();
        using var releaseUse = new ManualResetEventSlim();
        using var dropStarted = new ManualResetEventSlim();

        var useTask = Task.Run(() => supplier.TryUseClaimedTroop(
            "A",
            claimed.UniqueSeed,
            () =>
            {
                useStarted.Set();
                releaseUse.Wait();
                return true;
            }));
        useStarted.Wait();

        var dropTask = Task.Run(() =>
        {
            dropStarted.Set();
            return supplier.SetReserve(Array.Empty<PartyReserve>());
        });
        dropStarted.Wait();
        Assert.False(dropTask.IsCompleted);

        releaseUse.Set();
        Assert.Equal(CoopTroopSupplier.ClaimedTroopUseResult.Committed, await useTask);
        var dropped = await dropTask;

        var droppedParty = Assert.Single(dropped);
        Assert.Equal("A", droppedParty.PartyId);
        Assert.Equal(1, droppedParty.Supplied);
    }

    [Fact]
    public void FailedClaim_DroppedScopeReportsUncommittedPointerAndCancelsRetry()
    {
        var supplier = CreateResolvableSupplier("M1", "A", count: 3);
        Assert.True(supplier.TryClaimOneTroopFromParty("A", out var origin));
        var claimed = Assert.IsType<CoopAgentOrigin>(origin);
        Assert.Equal(
            CoopTroopSupplier.ClaimedTroopUseResult.Deferred,
            supplier.TryUseClaimedTroop("A", claimed.UniqueSeed, () => false));

        var dropped = supplier.SetReserve(Array.Empty<PartyReserve>());

        Assert.Equal(0, Assert.Single(dropped).Supplied);
        bool retried = false;
        Assert.Equal(
            CoopTroopSupplier.ClaimedTroopUseResult.ClaimMissing,
            supplier.TryUseClaimedTroop("A", claimed.UniqueSeed, () =>
            {
                retried = true;
                return true;
            }));
        Assert.False(retried);
    }

    [Fact]
    public void PhaseCapacityRecording_UsesTheCapturedSizingParties()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { Party("captured", 5) });
        var captured = supplier.GetSizingSnapshot();

        supplier.SetReserve(new[] { Party("newer", 3, seedBase: 700) });
        supplier.RecordPhaseCapacities(captured.PartyCapacities);

        Assert.Equal(5, supplier.GetRepresentedPhaseCapacity("captured"));
        Assert.Equal(0, supplier.GetRepresentedPhaseCapacity("newer"));
    }

    [Fact]
    public void PairSizingSnapshot_UsesTheSameLockOrderForEitherArgumentOrder()
    {
        var defender = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        var attacker = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);

        var forward = CoopTroopSupplier.OrderSizingLocks(defender, attacker);
        var reversed = CoopTroopSupplier.OrderSizingLocks(attacker, defender);

        Assert.Same(forward.First, reversed.First);
        Assert.Same(forward.Second, reversed.Second);
        Assert.NotSame(forward.First, forward.Second);
    }

    [Fact]
    public async Task PairSizingSnapshot_ConcurrentRefreshNeverMixesRevisionState()
    {
        var defender = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        var attacker = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        using var start = new ManualResetEventSlim();

        var defenderWriter = Task.Run(() => RefreshSizingStates(
            start,
            defender,
            Party("defender-odd", 20, initialSpawnCount: 7),
            Party("defender-even", 4, initialSpawnCount: 2)));
        var attackerWriter = Task.Run(() => RefreshSizingStates(
            start,
            attacker,
            Party("attacker-odd", 30, initialSpawnCount: 11),
            Party("attacker-even", 5, initialSpawnCount: 3)));
        var reader = Task.Run(() =>
        {
            start.Wait();
            for (int iteration = 0; iteration < 1000; iteration++)
            {
                CoopTroopSupplier.GetSizingSnapshots(defender, attacker, out var defenderSnapshot, out var attackerSnapshot);
                AssertSizingRevision(defenderSnapshot, oddTotal: 20, oddInitial: 7, evenTotal: 4, evenInitial: 2);
                AssertSizingRevision(attackerSnapshot, oddTotal: 30, oddInitial: 11, evenTotal: 5, evenInitial: 3);
            }
        });

        start.Set();
        await Task.WhenAll(defenderWriter, attackerWriter, reader);
    }

    private static void RefreshSizingStates(
        ManualResetEventSlim start,
        CoopTroopSupplier supplier,
        PartyReserve odd,
        PartyReserve even)
    {
        start.Wait();
        for (int revision = 1; revision <= 100; revision++)
            supplier.SetReserve(new[] { revision % 2 == 1 ? odd : even });
    }

    private static void AssertSizingRevision(
        CoopTroopSupplier.SizingSnapshot snapshot,
        int oddTotal,
        int oddInitial,
        int evenTotal,
        int evenInitial)
    {
        if (snapshot.ReserveRevision == 0)
        {
            Assert.False(snapshot.IsPopulated);
            Assert.Equal(0, snapshot.TotalTroops);
            Assert.Equal(0, snapshot.InitialTroops);
            return;
        }

        Assert.True(snapshot.IsPopulated);
        bool odd = snapshot.ReserveRevision % 2 == 1;
        Assert.Equal(odd ? oddTotal : evenTotal, snapshot.TotalTroops);
        Assert.Equal(odd ? oddInitial : evenInitial, snapshot.InitialTroops);
    }

    [Fact]
    public void SupplyOneTroopFromParty_AdvancesOnlyTheSelectedParty()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 3, supplied: 1), Party("B", 4) });

        supplier.SupplyOneTroopFromParty("B");
        supplier.SupplyOneTroopFromParty("B");

        Assert.Equal(1, SuppliedFor(supplier, "A"));
        Assert.Equal(2, SuppliedFor(supplier, "B"));
        Assert.Equal(2, supplier.GetRemainingForParty("A"));
        Assert.Equal(2, supplier.GetRemainingForParty("B"));
    }

    [Fact]
    public void SupplyOneTroopFromParty_MissingParty_DoesNotConsumeAnotherParty()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { Party("A", 3) });

        Assert.Null(supplier.SupplyOneTroopFromParty("missing"));

        Assert.Equal(0, SuppliedFor(supplier, "A"));
        Assert.Equal(3, supplier.GetRemainingForParty("A"));
    }
}
