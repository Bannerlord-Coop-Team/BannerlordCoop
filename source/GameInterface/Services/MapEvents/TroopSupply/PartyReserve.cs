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
    ///
    /// Additive with a default of 0, so a reserve from a build that does not send it still deserialises;
    /// 0 for every party simply reproduces the older proportional behaviour.
    /// </remarks>
    [ProtoMember(5)]
    public int SideOffset { get; }

    public PartyReserve(string partyId, int suppliedCount, TroopReserveEntry[] entries,
        bool isReceiverPlayerParty = false, int sideOffset = 0)
    {
        PartyId = partyId;
        SuppliedCount = suppliedCount;
        Entries = entries ?? Array.Empty<TroopReserveEntry>();
        IsReceiverPlayerParty = isReceiverPlayerParty;
        SideOffset = sideOffset;
    }
}
