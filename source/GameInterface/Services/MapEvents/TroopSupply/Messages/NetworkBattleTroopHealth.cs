using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Final surviving troop health sent before the owner's mission departure.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleTroopHealth : IEvent
{
    [ProtoMember(1)]
    public string MapEventId { get; }
    [ProtoMember(2)]
    public string PartyId { get; }
    [ProtoMember(3)]
    public Dictionary<int, float> Survivors { get; }
    [ProtoMember(4)]
    public int SuppliedCount { get; }
    [ProtoMember(5)]
    public Dictionary<int, float> RoutedSurvivors { get; }

    [ProtoMember(6)]
    public Guid SnapshotId { get; }

    public NetworkBattleTroopHealth(string mapEventId, string partyId, Dictionary<int, float> survivors, int suppliedCount, Dictionary<int, float> routedSurvivors = null, Guid snapshotId = default)
    {
        MapEventId = mapEventId;
        PartyId = partyId;
        Survivors = survivors;
        SuppliedCount = suppliedCount;
        RoutedSurvivors = routedSurvivors;
        SnapshotId = snapshotId;
    }
}
