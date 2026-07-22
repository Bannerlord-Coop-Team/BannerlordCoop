using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// One party's full ordered reserve, supplied pointer, and persistent initial-spawn entitlement.
/// Every authoritative snapshot carries the same frozen entitlement so migration can recover only its share.
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
    public int InitialSpawnCount { get; }
    [ProtoMember(5)]
    public int[] DepartedSeeds { get; }

    public PartyReserve(
        string partyId,
        int suppliedCount,
        TroopReserveEntry[] entries,
        int initialSpawnCount,
        int[] departedSeeds = null)
    {
        PartyId = partyId;
        SuppliedCount = suppliedCount;
        Entries = entries ?? Array.Empty<TroopReserveEntry>();
        InitialSpawnCount = initialSpawnCount;
        DepartedSeeds = departedSeeds ?? Array.Empty<int>();
    }
}
