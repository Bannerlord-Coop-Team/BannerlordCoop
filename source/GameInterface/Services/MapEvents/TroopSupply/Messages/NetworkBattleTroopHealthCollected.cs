using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Retire withdrawal snapshots before the server grants the rebuilt reserves.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleTroopHealthCollected : IEvent
{
    [ProtoMember(1)]
    public string MapEventId { get; }
    [ProtoMember(2)]
    public string[] PartyIds { get; }

    public NetworkBattleTroopHealthCollected(string mapEventId, string[] partyIds)
    {
        MapEventId = mapEventId;
        PartyIds = partyIds;
    }
}
