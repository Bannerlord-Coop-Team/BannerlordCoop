using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// One party's full ordered reserve plus how many of it have already been supplied. The supplied pointer is
/// what makes migration seamless: a fresh owner is handed the full list with the server's current pointer and
/// resumes exactly where the departed owner left off (so already-spawned troops aren't spawned again).
/// <see cref="IsReceiverPlayerParty"/> identifies the receiver's own authoritative map-event party without
/// asking the client to match divergent replicated campaign objects.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PartyReserve
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public int SuppliedCount { get; }
    [ProtoMember(3)]
    public TroopReserveEntry[] Entries { get; }
    [ProtoMember(4)]
    public bool IsReceiverPlayerParty { get; }

    /// <summary>
    /// Where this party's troops start within its SIDE, counting every party on that side in the order the
    /// server enumerates them - not just the ones this client owns.
    /// </summary>
    /// <remarks>
    /// This is what lets each owner compute a share that ADDS UP. Given the offset and the side total, an
    /// owner takes floor(alloc*(offset+count)/total) - floor(alloc*offset/total); because the offsets
    /// partition the side exactly once, those slices sum to the allocation across all owners with no
    /// coordination between them. Proportional rounding cannot do that - it overshoots or undershoots by a
    /// troop per owner - and a "never round down to zero" floor is worse still, spawning one troop per
    /// owner for a one-troop wave.
    /// </remarks>
    [ProtoMember(5)]
    public int SideOffset { get; }

    /// <summary>
    /// This party's position among the PLAYER-owned parties on its side, in the server's enumeration order,
    /// or -1 when it is not one.
    /// </summary>
    /// <remarks>
    /// Together with <see cref="SideReserve.PlayerOwnedPartyCount"/> this is what lets every owner guarantee
    /// its player an agent WITHOUT overshooting the allocation. The share is computed as: reserve one troop
    /// for each of the side's player-owned parties, remove those troops from the party intervals, apportion
    /// what remains by cumulative flooring, and add the reserved troop back for the party this
    /// client owns. Those pieces sum to exactly the allocation, because the flooring covers the remainder
    /// exactly once and there are exactly as many reserved troops as player-owned parties.
    ///
    /// The rank matters only when the allocation is smaller than the number of player-owned parties, where
    /// there is not one troop to go round: the first <c>allocation</c> ranks get the troop and the rest get
    /// none, which every client agrees on because the ranks come from the server.
    /// </remarks>
    [ProtoMember(6)]
    public int PlayerOwnedRank { get; }

    /// <summary>This party's side offset after one reserved troop is removed from each preceding player party.</summary>
    [ProtoMember(7)]
    public int UnreservedSideOffset { get; }

    /// <summary>Whether <see cref="UnreservedSideOffset"/> was supplied by the server.</summary>
    [ProtoMember(8)]
    public bool HasUnreservedSideOffset { get; }

    public PartyReserve(string partyId, int suppliedCount, TroopReserveEntry[] entries,
        bool isReceiverPlayerParty = false, int sideOffset = 0, int playerOwnedRank = -1,
        int unreservedSideOffset = 0, bool hasUnreservedSideOffset = false)
    {
        PartyId = partyId;
        SuppliedCount = suppliedCount;
        Entries = entries ?? Array.Empty<TroopReserveEntry>();
        IsReceiverPlayerParty = isReceiverPlayerParty;
        SideOffset = sideOffset;
        PlayerOwnedRank = playerOwnedRank;
        UnreservedSideOffset = unreservedSideOffset;
        HasUnreservedSideOffset = hasUnreservedSideOffset;
    }
}
