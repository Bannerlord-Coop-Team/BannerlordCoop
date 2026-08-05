using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>
/// Server → owning client: the authoritative reserve for one battle SIDE — every party on that side the
/// client owns (its own party; or, for the host, the AI/enemy parties), each with the server's current
/// supplied pointer. The client sets it on that side's <see cref="CoopTroopSupplier"/>. Sent for BOTH sides
/// (an empty <see cref="Parties"/> array means "you own nothing on this side", which still completes its
/// deployment). Re-sent on migration carrying the remaining pointer so the new owner resumes cleanly.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleTroopReserve : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly int Side;
    [ProtoMember(3)]
    public readonly PartyReserve[] Parties = Array.Empty<PartyReserve>();

    /// <summary>
    /// The server needs the receiver's FINAL supplied pointers for every party this REPLACE takes away
    /// (BR-033 shrink refresh): the receiver must answer with a <see cref="NetworkBattleSupplyProgress"/>
    /// whose <c>IsFlush</c> is set — one ack per flagged message — BEFORE the dropped parties can be
    /// re-issued to their returning owner. The server's ledger lags the holder's true local pointer by up
    /// to one throttled report interval, so serving the returner without this handshake can re-issue
    /// descriptors the holder already fielded (duplicate agents sharing one UniqueSeed). Additive and
    /// default-false, so a legacy peer simply applies the REPLACE and never acks (the server's deadline
    /// fallback then serves the returner from the ledger, accepting today's race).
    /// </summary>
    [ProtoMember(4)]
    public readonly bool FlushRequested;

    /// <summary>
    /// Every troop on this side across ALL owners, not just the ones in <see cref="Parties"/>.
    /// <para>
    /// The spawn logic splits a fixed battle size between the two sides in proportion to the totals it is
    /// given, so a client that sizes from what it happens to OWN computes a different split from its peers:
    /// a side divided between two players is measured at a fraction of its real strength, its opponent gets
    /// capped against that fraction, and the divided side ends up fielding more men than the larger one.
    /// Feeding every client the same side totals makes the split identical everywhere; each supplier then
    /// contributes only its own share of that allocation.
    /// </para>
    /// Additive and default 0, which reads as "unknown" and falls back to sizing from owned totals.
    /// </summary>
    [ProtoMember(5)]
    public readonly int SideTotalTroops;

    public NetworkBattleTroopReserve(string mapEventId, int side, PartyReserve[] parties, bool flushRequested = false,
        int sideTotalTroops = 0)
    {
        MapEventId = mapEventId;
        Side = side;
        Parties = parties;
        FlushRequested = flushRequested;
        SideTotalTroops = sideTotalTroops;
    }
}
