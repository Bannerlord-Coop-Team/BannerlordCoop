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

    public NetworkBattleSupplyProgress(string mapEventId, SupplyProgressEntry[] entries)
    {
        MapEventId = mapEventId;
        Entries = entries;
    }
}
