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

    private static PartyReserve Party(string id, int count, int supplied = 0, int seedBase = 500)
        => new PartyReserve(id, supplied, Entries(count, seedBase));

    private static int SuppliedFor(CoopTroopSupplier supplier, string partyId)
        => supplier.GetSuppliedByParty().First(p => p.partyId == partyId).supplied;

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

        supplier.SetReserve(new[] { new PartyReserve("A", 3, Entries(10)) });

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

        supplier.SetReserve(new[] { new PartyReserve("A", 6, Entries(10)) });

        Assert.Equal(6, SuppliedFor(supplier, "A"));
    }

    [Fact]
    public void SetReserve_WithSuppliedPointer_ResumesMidway()
    {
        // Migration: a new owner is handed the full list at the server's pointer and continues from there.
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[] { new PartyReserve("A", 7, Entries(10)) });

        Assert.Equal(3, supplier.NumTroopsNotSupplied);
        Assert.Equal(7, SuppliedFor(supplier, "A"));

        supplier.SupplyTroops(2);
        Assert.Equal(9, SuppliedFor(supplier, "A"));
    }
}
