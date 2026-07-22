using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>
/// Server → owning client: the authoritative reserve for one battle SIDE — every party on that side the
/// client owns (its own party; or, for the host, the AI/enemy parties), each with the server's current
/// supplied pointer and persistent initial-spawn entitlement. The client sets it on that side's
/// <see cref="CoopTroopSupplier"/>. Complete election/migration grants are sent for BOTH sides (an empty
/// <see cref="Parties"/> array means "you own nothing on this side"); entry grants intentionally omit empty
/// sides until the client's mission-ready role is final. Re-sent on migration carrying the remaining pointer
/// so the new owner resumes cleanly.
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
    /// descriptors the holder already fielded (duplicate agents sharing one UniqueSeed). Unflagged reserve
    /// replacements do not require an acknowledgement.
    /// </summary>
    [ProtoMember(4)]
    public readonly bool FlushRequested;

    /// <summary>
    /// Monotonic id shared by every side in one logical reserve grant. Initial spawn sizing accepts two
    /// populated suppliers only when this id matches, so it cannot combine an entry reserve with the first
    /// side of a later election grant.
    /// </summary>
    [ProtoMember(5)]
    public readonly long GrantGeneration;

    /// <summary>Whether this grant explicitly finalizes both sides for initial mission sizing.</summary>
    [ProtoMember(6)]
    public readonly bool CompletesInitialSizing;

    public NetworkBattleTroopReserve(
        string mapEventId,
        int side,
        PartyReserve[] parties,
        bool flushRequested = false,
        long grantGeneration = 0,
        bool completesInitialSizing = false)
    {
        MapEventId = mapEventId;
        Side = side;
        Parties = parties;
        FlushRequested = flushRequested;
        GrantGeneration = grantGeneration;
        CompletesInitialSizing = completesInitialSizing;
    }
}
