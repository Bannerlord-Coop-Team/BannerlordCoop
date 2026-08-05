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
        bool isReceiverPlayerParty = false)
        => new PartyReserve(id, supplied, Entries(count, seedBase), isReceiverPlayerParty);

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
        supplier.SetReserve(Array.Empty<PartyReserve>());

        Assert.False(supplier.AnyTroopRemainsToBeSupplied);
        Assert.Equal(0, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void SetReserve_ThenSupply_AdvancesPerPartyPointer()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 10) });

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
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 2, seedBase: 100), Party("B", 3, seedBase: 200) });

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
            Party("player", 2, seedBase: 200, isReceiverPlayerParty: true),
        });

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
        supplier.SetReserve(new[] { Party("A", 10) });
        supplier.SupplyTroops(5);
        Assert.Equal(5, SuppliedFor(supplier, "A"));

        supplier.SetReserve(new[] { new PartyReserve("A", 3, Entries(10)) });

        Assert.Equal(5, SuppliedFor(supplier, "A"));
        Assert.Equal(5, supplier.NumTroopsNotSupplied);
    }

    [Fact]
    public void Resend_WithHigherPointer_AdvancesToServer()
    {
        // The normal migration resume: a party the server is further along on (or one we hadn't supplied
        // locally) takes the server's higher pointer.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 10) });
        supplier.SupplyTroops(2);

        supplier.SetReserve(new[] { new PartyReserve("A", 6, Entries(10)) });

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
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { new PartyReserve("A", 7, Entries(10)) });

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

        supplier.SetReserve(new[] { Party("A", 2) });
        supplier.SetReserve(new[] { Party("A", 2) });

        Assert.Equal(2, supplier.ReserveRevision);
    }

    [Fact]
    public void SupplyOneTroopFromParty_AdvancesOnlyTheSelectedParty()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
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
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("A", 3) });

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
    public void SideTotal_DefaultsToOwned_WhenServerSendsNone()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 382) });

        Assert.Equal(382, supplier.SideTotalTroops);
        Assert.Equal(100, supplier.OwnedShareOf(100)); // sole owner: the whole allocation
    }

    [Fact]
    public void SideTotal_UsesServerValue_AndSharesAllocationByOwnership()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 382) }, sideTotal: 955);

        Assert.Equal(955, supplier.SideTotalTroops);
        Assert.Equal(160, supplier.OwnedShareOf(400)); // 400 * 382/955 = 160.0
    }

    [Fact]
    public void OwnersShares_SumToTheAllocation_WithoutOverSpawning()
    {
        var mine = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        mine.SetReserve(new[] { Party("P1", 382) }, sideTotal: 955);

        var theirs = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        theirs.SetReserve(new[] { Party("P2", 573, seedBase: 9000) }, sideTotal: 955);

        const int allocation = 400;
        var combined = mine.OwnedShareOf(allocation) + theirs.OwnedShareOf(allocation);

        // Rounding may move a single troop either way; what must never happen is the side being fielded
        // once per owner, which is what the bug did.
        Assert.InRange(combined, allocation - 1, allocation + 1);
        Assert.True(combined < allocation * 2);
    }

    [Fact]
    public void SoleOwnerOfASide_StillGetsTheWholeAllocation()
    {
        var host = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null, new BattleAgentBudget());
        host.SetReserve(new[] { Party("AI", 1400) }, sideTotal: 1400);

        Assert.Equal(600, host.OwnedShareOf(600));
    }

    [Fact]
    public void SmallAllocation_NeverRoundsAnOwnerDownToNothing()
    {
        // A zero share on the side holding the local player's party reads as "origin missing" to the spawn
        // handler, which aborts the battle - the failure this must not reintroduce.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 10) }, sideTotal: 1000);

        Assert.Equal(1, supplier.OwnedShareOf(1));
        Assert.True(supplier.OwnedShareOf(5) >= 1);
    }

    [Fact]
    public void ZeroSideTotal_FromAnOlderServer_LeavesEarlierValueIntact()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { Party("P1", 382) }, sideTotal: 955);
        supplier.SetReserve(new[] { Party("P1", 382) }); // resend with no total

        Assert.Equal(955, supplier.SideTotalTroops);
    }
}
