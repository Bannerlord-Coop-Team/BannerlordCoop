using GameInterface.Services.MapEvents.TroopSupply;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests for <see cref="BattleTroopLedger"/> — the server-authoritative reserve + supplied-pointer that
/// makes disconnect/host-migration seamless (a new owner resumes from the pointer). Pure logic, no game.
/// </summary>
public class BattleTroopLedgerTests
{
    private const string Battle = "MapEvent_1";
    private const string Party = "Party_A";

    private static TroopReserveEntry[] Reserve(int count)
    {
        var entries = new TroopReserveEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = new TroopReserveEntry(seed: 1000 + i, characterId: $"Char_{i}", formationClass: i % 4);
        return entries;
    }

    [Fact]
    public void SetReserve_RoundTrips_WithZeroSupplied()
    {
        var ledger = new BattleTroopLedger();
        var entries = Reserve(5);

        ledger.SetReserve(Battle, Party, entries);

        Assert.True(ledger.TryGetReserve(Battle, Party, out var stored, out var supplied));
        Assert.Equal(5, stored.Count);
        Assert.Equal(0, supplied);
        Assert.Equal(1000, stored[0].Seed);
        Assert.Equal("Char_0", stored[0].CharacterId);
    }

    [Fact]
    public void UnknownBattleOrParty_ReturnsFalse_AndEmpty()
    {
        var ledger = new BattleTroopLedger();
        ledger.SetReserve(Battle, Party, Reserve(3));

        Assert.False(ledger.TryGetReserve("nope", Party, out _, out _));
        Assert.False(ledger.TryGetReserve(Battle, "nope", out _, out _));
        Assert.Empty(ledger.GetRemaining("nope", Party));
    }

    [Fact]
    public void ReportSupplied_AdvancesPointer_AndIsMonotonic()
    {
        var ledger = new BattleTroopLedger();
        ledger.SetReserve(Battle, Party, Reserve(10));

        ledger.ReportSupplied(Battle, Party, 4);
        Assert.True(ledger.TryGetReserve(Battle, Party, out _, out var supplied));
        Assert.Equal(4, supplied);

        // A stale/lower report never rewinds the pointer.
        ledger.ReportSupplied(Battle, Party, 2);
        Assert.True(ledger.TryGetReserve(Battle, Party, out _, out supplied));
        Assert.Equal(4, supplied);

        ledger.ReportSupplied(Battle, Party, 7);
        Assert.True(ledger.TryGetReserve(Battle, Party, out _, out supplied));
        Assert.Equal(7, supplied);
    }

    [Fact]
    public void ReportSupplied_IsClampedToReserveSize()
    {
        var ledger = new BattleTroopLedger();
        ledger.SetReserve(Battle, Party, Reserve(3));

        ledger.ReportSupplied(Battle, Party, 99);

        Assert.True(ledger.TryGetReserve(Battle, Party, out _, out var supplied));
        Assert.Equal(3, supplied);
        Assert.Empty(ledger.GetRemaining(Battle, Party));
    }

    [Fact]
    public void GetRemaining_ReturnsTailFromPointer_ForANewOwner()
    {
        var ledger = new BattleTroopLedger();
        ledger.SetReserve(Battle, Party, Reserve(6));
        ledger.ReportSupplied(Battle, Party, 4);

        var remaining = ledger.GetRemaining(Battle, Party);

        Assert.Equal(2, remaining.Count);
        Assert.Equal(1004, remaining[0].Seed); // entry index 4 onward
        Assert.Equal(1005, remaining[1].Seed);
    }

    [Fact]
    public void SetReserve_ResetsTheSuppliedPointer()
    {
        var ledger = new BattleTroopLedger();
        ledger.SetReserve(Battle, Party, Reserve(5));
        ledger.ReportSupplied(Battle, Party, 3);

        ledger.SetReserve(Battle, Party, Reserve(5));

        Assert.True(ledger.TryGetReserve(Battle, Party, out _, out var supplied));
        Assert.Equal(0, supplied);
    }

    [Fact]
    public void GetParties_ListsPartiesWithReserves_AndRemoveClearsBattle()
    {
        var ledger = new BattleTroopLedger();
        ledger.SetReserve(Battle, "Party_A", Reserve(2));
        ledger.SetReserve(Battle, "Party_B", Reserve(2));

        var parties = ledger.GetParties(Battle);
        Assert.Equal(2, parties.Count);
        Assert.Contains("Party_A", parties);
        Assert.Contains("Party_B", parties);

        ledger.Remove(Battle);
        Assert.Empty(ledger.GetParties(Battle));
    }
}
