using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Server to client: clear transient priority-slot state before replaying the authoritative snapshot.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePrioritySnapshotReset : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkBattlePrioritySnapshotReset(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
