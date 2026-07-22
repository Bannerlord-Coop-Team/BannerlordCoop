using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Server to host: snapshot current reserves before the following refresh adds newly-owned parties.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleReserveOwnershipExpanded : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkBattleReserveOwnershipExpanded(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
