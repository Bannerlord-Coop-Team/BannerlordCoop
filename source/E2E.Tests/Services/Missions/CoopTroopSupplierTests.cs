using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using System.Linq;
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

    private static PartyReserve Party(string id, int count, int supplied = 0, int seedBase = 500,
        bool isReceiverPlayerParty = false, int sideOffset = 0, int playerOwnedRank = -1)
        => new PartyReserve(id, supplied, Entries(count, seedBase), isReceiverPlayerParty, sideOffset,
            playerOwnedRank);

    private static int SuppliedFor(CoopTroopSupplier supplier, string partyId)
        => supplier.GetSuppliedByParty().First(p => p.partyId == partyId).supplied;

    [Fact]
    public void BeforePopulated_ReportsTroopsStillComing_AndSuppliesNone()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());

        Assert.True(supplier.AnyTroopRemainsToBeSupplied); // not populated -> deployment must wait
        Assert.Empty(supplier.SupplyTroops(5));
    }

    [Fact]
    public void EmptyReserve_MarksPopulated_AndReportsDone()
    {
        // A side this client owns nothing on still gets an (empty) reserve, so deployment completes.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(Array.Empty<PartyReserve>(), sideTotal: 0, playerOwnedParties: 0);

        Assert.False(supplier.AnyTroopRemainsToBeSupplied);
        Assert.Equal(0, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void SetReserve_ThenSupply_AdvancesPerPartyPointer()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 10) }, sideTotal: 10, playerOwnedParties: 0);

        Assert.Equal(10, supplier.NumTroopsNotSupplied);

        supplier.SupplyTroops(4);
        Assert.Equal(6, supplier.NumTroopsNotSupplied);
        Assert.Equal(4, SuppliedFor(supplier, "A"));

        supplier.SupplyOneTroop();
        Assert.Equal(5, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void SupplyingPastEnd_StopsAndReportsExhausted()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 3) }, sideTotal: 3, playerOwnedParties: 0);

        supplier.SupplyTroops(99);

        Assert.Equal(0, supplier.NumTroopsNotSupplied);
        Assert.False(supplier.AnyTroopRemainsToBeSupplied);
        Assert.Null(supplier.SupplyOneTroop());
        Assert.Equal(3, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void Supply_SpansMultipleParties_InOrder()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 2, seedBase: 100), Party("B", 3, seedBase: 200) },
            sideTotal: 5, playerOwnedParties: 0);

        Assert.Equal(5, supplier.NumTroopsNotSupplied);

        supplier.SupplyTroops(4); // 2 from A, then 2 from B
        Assert.Equal(2, SuppliedFor(supplier, "A"));
        Assert.Equal(2, SuppliedFor(supplier, "B"));
    }

    [Fact]
    public void Supply_PrioritizesLocalPartyBeforeOtherOwnedParties()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[]
        {
            Party("army-member", 3, seedBase: 100),
            Party("player", 2, seedBase: 200, isReceiverPlayerParty: true, playerOwnedRank: 0),
        }, sideTotal: 5, playerOwnedParties: 1);

        supplier.SupplyTroops(1);

        Assert.Equal(0, SuppliedFor(supplier, "army-member"));
        Assert.Equal(1, SuppliedFor(supplier, "player"));
    }

    [Fact]
    public void StaleResend_DoesNotRewind_FurtherAlongPointer()
    {
        // Migration/race: we've already supplied 5, but a resend carries a STALE pointer (3) because our last
        // progress report hasn't reached the server's ledger yet. Re-applying it must NOT rewind to 3 (which
        // would re-spawn troops 4 and 5, already on the field, with duplicate seeds) — the pointer is monotonic.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 10) }, sideTotal: 10, playerOwnedParties: 0);
        supplier.SupplyTroops(5);
        Assert.Equal(5, SuppliedFor(supplier, "A"));

        supplier.SetReserve(new[] { new PartyReserve("A", 3, Entries(10)) },
            sideTotal: 10, playerOwnedParties: 0);

        Assert.Equal(5, SuppliedFor(supplier, "A"));
        Assert.Equal(5, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void Resend_WithHigherPointer_AdvancesToServer()
    {
        // The normal migration resume: a party the server is further along on (or one we hadn't supplied
        // locally) takes the server's higher pointer.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 10) }, sideTotal: 10, playerOwnedParties: 0);
        supplier.SupplyTroops(2);

        supplier.SetReserve(new[] { new PartyReserve("A", 6, Entries(10)) },
            sideTotal: 10, playerOwnedParties: 0);

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
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("returned", 4, seedBase: 100), Party("kept", 3, seedBase: 200) },
            sideTotal: 7, playerOwnedParties: 0);
        supplier.SupplyTroops(1); // pointer advanced on "returned" before the shrink lands

        supplier.SetReserve(new[] { Party("kept", 3, seedBase: 200) },
            sideTotal: 3, playerOwnedParties: 0);

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
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { new PartyReserve("A", 7, Entries(10)) },
            sideTotal: 10, playerOwnedParties: 0);

        Assert.Equal(3, supplier.NumTroopsNotSupplied);
        Assert.Equal(7, SuppliedFor(supplier, "A"));

        supplier.SupplyTroops(2);
        Assert.Equal(9, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void SetReserve_IncrementsRevision_ForEachAuthoritativeSnapshot()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());

        Assert.Equal(0, supplier.ReserveRevision);

        supplier.SetReserve(new[] { Party("A", 2) }, sideTotal: 2, playerOwnedParties: 0);
        supplier.SetReserve(new[] { Party("A", 2) }, sideTotal: 2, playerOwnedParties: 0);

        Assert.Equal(2, supplier.ReserveRevision);
    }

    [Fact]
    public void SupplyOneTroopFromParty_AdvancesOnlyTheSelectedParty()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 3, supplied: 1), Party("B", 4) },
            sideTotal: 7, playerOwnedParties: 0);

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
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 3) }, sideTotal: 3, playerOwnedParties: 0);

        Assert.Null(supplier.SupplyOneTroopFromParty("missing"));

        Assert.Equal(0, SuppliedFor(supplier, "A"));
        Assert.Equal(3, supplier.GetRemainingForParty("A"));
    }

    // --- side totals and per-owner shares -------------------------------------------------------------
    // The engine splits a fixed battle size in proportion to the totals it is handed, and it asks EVERY
    // client for the whole side's allocation. Sizing from what a client happens to own made a side divided
    // between two players measure smaller than it is: its opponent was capped against that fraction while
    // the divided side kept filling from each owner. Live case that produced it: attacker 955 across two
    // clients (382 here), defender 1400 held by the host -> the split came out 1400:382 instead of 1400:955,
    // capping the defenders at 300 while the attackers fielded 673.

    [Fact]
    public void SideTotal_UsesServerValue_AndSharesAllocationByOwnership()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 382) }, sideTotal: 955, playerOwnedParties: 0);

        Assert.Equal(955, supplier.SideTotalTroops);
        Assert.Equal(160, supplier.OwnedShareOf(400)); // 400 * 382/955 = 160.0
    }

    [Fact]
    public void OwnersShares_SumToTheAllocation_WithoutOverSpawning()
    {
        var mine = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        mine.SetReserve(new[] { Party("P1", 382) }, sideTotal: 955, playerOwnedParties: 0);

        var theirs = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        theirs.SetReserve(new[] { Party("P2", 573, seedBase: 9000, sideOffset: 382) },
            sideTotal: 955, playerOwnedParties: 0);

        const int allocation = 400;
        var combined = mine.OwnedShareOf(allocation) + theirs.OwnedShareOf(allocation);

        // EXACTLY the allocation. The offsets partition the side, so cumulative flooring apportions it
        // with nothing lost and nothing duplicated - no owner needs to know what the other holds.
        Assert.Equal(allocation, combined);
    }

    [Fact]
    public void ThreePlayers_ReceiveTroopsProportionalToEligibleCounts()
    {
        const int total = 1000;
        const int allocation = 600;
        const int playerParties = 3;

        var largest = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        largest.SetReserve(new[] { Party("L", 600, isReceiverPlayerParty: true, playerOwnedRank: 0) },
            sideTotal: total, playerOwnedParties: playerParties);
        var middle = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        middle.SetReserve(new[] { Party("M", 300, seedBase: 4000, isReceiverPlayerParty: true,
            sideOffset: 600, playerOwnedRank: 1) }, sideTotal: total, playerOwnedParties: playerParties);
        var smallest = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        smallest.SetReserve(new[] { Party("S", 100, seedBase: 8000, isReceiverPlayerParty: true,
            sideOffset: 900, playerOwnedRank: 2) }, sideTotal: total, playerOwnedParties: playerParties);

        var shares = new[]
        {
            largest.OwnedShareOf(allocation),
            middle.OwnedShareOf(allocation),
            smallest.OwnedShareOf(allocation),
        };

        Assert.Equal(new[] { 359, 180, 61 }, shares);
        Assert.Equal(allocation, shares.Sum());
        Assert.InRange(shares[0], 359, 361);
        Assert.InRange(shares[1], 179, 181);
        Assert.InRange(shares[2], 59, 61);
    }

    [Fact]
    public void ExactApportionment_HoldsForAwkwardSplits()
    {
        // Deliberately indivisible: three owners, a total and an allocation that share no clean factor.
        const int total = 1000;
        const int allocation = 7;

        var a = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        a.SetReserve(new[] { Party("A", 333, sideOffset: 0) }, sideTotal: total, playerOwnedParties: 0);
        var b = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        b.SetReserve(new[] { Party("B", 333, seedBase: 4000, sideOffset: 333) },
            sideTotal: total, playerOwnedParties: 0);
        var c = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        c.SetReserve(new[] { Party("C", 334, seedBase: 8000, sideOffset: 666) },
            sideTotal: total, playerOwnedParties: 0);

        Assert.Equal(allocation,
            a.OwnedShareOf(allocation) + b.OwnedShareOf(allocation) + c.OwnedShareOf(allocation));
    }

    // --- exact apportionment with a guaranteed troop per player ---------------------------------------
    // Reserving one troop per player-owned party BEFORE the proportional split is what lets both invariants
    // hold at once. The older top-up added its troop AFTER, so each owner that floored to zero raised itself
    // to one with no idea the others were doing the same, and the side fielded more men than the engine
    // asked for. The reviewer's case: an owner holding 1 troop of a 1000-strong side floors to zero for an
    // allocation of 100 and tops up, while the other owners already supply all 100.

    [Fact]
    public void GuaranteedTroopPerPlayer_KeepsTheAllocationExact_OnLopsidedOwnership()
    {
        const int total = 1000;
        const int allocation = 100;
        const int playerParties = 2;

        // One troop of 1000 - the share that used to round to zero and then top itself up.
        var tiny = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        tiny.SetReserve(new[] { Party("TINY", 1, isReceiverPlayerParty: true, sideOffset: 0, playerOwnedRank: 0) },
            sideTotal: total, playerOwnedParties: playerParties);

        var big = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        big.SetReserve(new[] { Party("BIG", 999, seedBase: 4000, isReceiverPlayerParty: true, sideOffset: 1,
            playerOwnedRank: 1) }, sideTotal: total, playerOwnedParties: playerParties);

        Assert.Equal(allocation, tiny.OwnedShareOf(allocation) + big.OwnedShareOf(allocation));
        Assert.True(tiny.OwnedShareOf(allocation) >= 1, "the small owner still fields its player");
    }

    [Fact]
    public void GuaranteedTroopPerPlayer_ExactAcrossThreeOwners_IncludingAnAiOwner()
    {
        const int total = 1000;
        const int playerParties = 2;

        var p1 = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        p1.SetReserve(new[] { Party("P1", 1, isReceiverPlayerParty: true, sideOffset: 0, playerOwnedRank: 0) },
            sideTotal: total, playerOwnedParties: playerParties);
        var p2 = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        p2.SetReserve(new[] { Party("P2", 2, seedBase: 3000, isReceiverPlayerParty: true, sideOffset: 1,
            playerOwnedRank: 1) }, sideTotal: total, playerOwnedParties: playerParties);
        var ai = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        ai.SetReserve(new[] { Party("AI", 997, seedBase: 7000, sideOffset: 3) },
            sideTotal: total, playerOwnedParties: playerParties);

        foreach (var allocation in new[] { 2, 3, 7, 50, 100, 337, 999 })
        {
            Assert.Equal(allocation,
                p1.OwnedShareOf(allocation) + p2.OwnedShareOf(allocation) + ai.OwnedShareOf(allocation));
        }
    }

    [Fact]
    public void WaveTooSmallForEveryPlayer_GivesItToTheLowestRanks_AndStaysExact()
    {
        const int total = 1000;
        const int playerParties = 3;

        var r0 = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        r0.SetReserve(new[] { Party("R0", 10, isReceiverPlayerParty: true, sideOffset: 0, playerOwnedRank: 0) },
            sideTotal: total, playerOwnedParties: playerParties);
        var r1 = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        r1.SetReserve(new[] { Party("R1", 10, seedBase: 3000, isReceiverPlayerParty: true, sideOffset: 10,
            playerOwnedRank: 1) }, sideTotal: total, playerOwnedParties: playerParties);
        var r2 = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        r2.SetReserve(new[] { Party("R2", 10, seedBase: 6000, isReceiverPlayerParty: true, sideOffset: 20,
            playerOwnedRank: 2) }, sideTotal: total, playerOwnedParties: playerParties);

        // Two troops, three players: ranks 0 and 1 get them, rank 2 waits for the next wave. Every client
        // reaches the same answer because the ranks come from the server.
        Assert.Equal(1, r0.OwnedShareOf(2));
        Assert.Equal(1, r1.OwnedShareOf(2));
        Assert.Equal(0, r2.OwnedShareOf(2));
        Assert.Equal(2, r0.OwnedShareOf(2) + r1.OwnedShareOf(2) + r2.OwnedShareOf(2));
    }

    [Fact]
    public void SoleOwnerOfASide_StillGetsTheWholeAllocation()
    {
        var host = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        host.SetReserve(new[] { Party("AI", 1400) }, sideTotal: 1400, playerOwnedParties: 0);

        Assert.Equal(600, host.OwnedShareOf(600));
    }

    [Fact]
    public void SmallAllocation_StillFieldsTheReceiversOwnParty()
    {
        // A zero share on the side holding the local player's party reads as "origin missing" to the spawn
        // handler, which aborts the battle - the failure this must not reintroduce.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 10, isReceiverPlayerParty: true, playerOwnedRank: 0) },
            sideTotal: 1000, playerOwnedParties: 1);

        Assert.Equal(1, supplier.OwnedShareOf(1));
        Assert.True(supplier.OwnedShareOf(5) >= 1);
    }

    [Fact]
    public void SmallAllocation_DoesNotFieldOneTroopPerOwner()
    {
        // The counterpart to the test above, and the reason the floor is no longer applied to everyone:
        // an owner without the receiver's party takes its apportioned share even when that is nothing.
        // Topping every owner up to one turned a one-troop wave into one troop PER OWNER.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("AI", 10, sideOffset: 500) },
            sideTotal: 1000, playerOwnedParties: 0);

        Assert.Equal(0, supplier.OwnedShareOf(1));
    }

    [Fact]
    public void NextSnapshot_CanClearThePlayerPartyCount()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 10, isReceiverPlayerParty: true, playerOwnedRank: 0) },
            sideTotal: 100, playerOwnedParties: 1);

        supplier.SetReserve(new[] { Party("AI", 100) }, sideTotal: 100, playerOwnedParties: 0);

        Assert.Equal(0, supplier.PlayerOwnedPartyCount);
    }

    [Fact]
    public void EmptySnapshot_ClearsAuthoritativeTotals()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 10, isReceiverPlayerParty: true, playerOwnedRank: 0) },
            sideTotal: 10, playerOwnedParties: 1);

        supplier.SetReserve(Array.Empty<PartyReserve>(), sideTotal: 0, playerOwnedParties: 0);

        Assert.Equal(0, supplier.SideTotalTroops);
        Assert.Equal(0, supplier.PlayerOwnedPartyCount);
        Assert.Equal(0, supplier.OwnedShareOf(10));
    }

    [Fact]
    public void AllocationSnapshot_RemainsInternallyStable_AfterTheNextGenerationArrives()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[]
        {
            Party("P1", 40, supplied: 3, isReceiverPlayerParty: true, sideOffset: 0, playerOwnedRank: 0),
        }, sideTotal: 100, playerOwnedParties: 2, snapshotRevision: 2);
        var generationTwo = supplier.CaptureAllocationSnapshot();

        supplier.SetReserve(new[]
        {
            Party("P1", 50, supplied: 7, isReceiverPlayerParty: true, sideOffset: 150, playerOwnedRank: 1),
        }, sideTotal: 200, playerOwnedParties: 2, snapshotRevision: 3);
        var generationThree = supplier.CaptureAllocationSnapshot();

        Assert.Equal(2, generationTwo.Revision);
        Assert.Equal(100, generationTwo.SideTotalTroops);
        Assert.Equal(40, generationTwo.TotalTroops);
        Assert.Equal(3, generationTwo.SuppliedTroops);
        Assert.Equal(20, generationTwo.OwnedShareOf(50));

        Assert.Equal(3, generationThree.Revision);
        Assert.Equal(200, generationThree.SideTotalTroops);
        Assert.Equal(50, generationThree.TotalTroops);
        Assert.Equal(7, generationThree.SuppliedTroops);
        Assert.Equal(13, generationThree.OwnedShareOf(50));
    }
}
