using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Collect the holder's remaining health before rebuilding a withdrawn party's reserve.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkRequestBattleTroopHealth : IEvent
{
    [ProtoMember(1)]
    public string MapEventId { get; }
    [ProtoMember(2)]
    public string[] PartyIds { get; }
    [ProtoMember(3)]
    public Guid SnapshotId { get; }

    public NetworkRequestBattleTroopHealth(string mapEventId, string[] partyIds, Guid snapshotId)
    {
        MapEventId = mapEventId;
        PartyIds = partyIds;
        SnapshotId = snapshotId;
    }
}
