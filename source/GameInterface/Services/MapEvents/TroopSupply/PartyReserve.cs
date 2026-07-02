using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// One party's full ordered reserve plus how many of it have already been supplied. The supplied pointer is
/// what makes migration seamless: a fresh owner is handed the full list with the server's current pointer and
/// resumes exactly where the departed owner left off (so already-spawned troops aren't spawned again).
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

    public PartyReserve(string partyId, int suppliedCount, TroopReserveEntry[] entries)
    {
        PartyId = partyId;
        SuppliedCount = suppliedCount;
        Entries = entries ?? Array.Empty<TroopReserveEntry>();
    }
}
