using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>One party's supplied pointer, reported by its owning client.</summary>
[ProtoContract(SkipConstructor = true)]
public class SupplyProgressEntry
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public int SuppliedCount { get; }

    public SupplyProgressEntry(string partyId, int suppliedCount)
    {
        PartyId = partyId;
        SuppliedCount = suppliedCount;
    }
}

/// <summary>
/// Owning client → server: how many troops it has supplied so far for each party it owns. The server advances
/// its <c>IBattleTroopLedger</c> pointer (monotonically), so when that client disconnects the new owner can be
/// handed the remaining tail. Sent periodically from the battle controller's tick (game thread).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleSupplyProgress : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly SupplyProgressEntry[] Entries = Array.Empty<SupplyProgressEntry>();

    /// <summary>
    /// This report is the FLUSH ACK for a <see cref="NetworkBattleTroopReserve"/> whose
    /// <c>FlushRequested</c> was set (BR-033 shrink refresh): <see cref="Entries"/> carries the sender's
    /// FINAL local pointers for the parties that REPLACE dropped, captured atomically with the replace, so
    /// the server can land them in the ledger and only then serve the returning owner. Distinguished from
    /// the periodic throttled reports so the server can count acks against a pending return; the pointer
    /// application itself stays idempotent either way (the ledger is monotonic + clamped). Additive and
    /// default-false, so legacy senders keep plain periodic-report semantics.
    /// </summary>
    [ProtoMember(3)]
    public readonly bool IsFlush;

    public NetworkBattleSupplyProgress(string mapEventId, SupplyProgressEntry[] entries, bool isFlush = false)
    {
        MapEventId = mapEventId;
        Entries = entries;
        IsFlush = isFlush;
    }
}
