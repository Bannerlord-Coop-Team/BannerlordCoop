using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

[ProtoContract]
public readonly struct NetworkTroopKilled : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly string TroopId;

    public NetworkTroopKilled(string mapEventPartyId, string troopId)
    {
        MapEventPartyId = mapEventPartyId;
        TroopId = troopId;
    }
}