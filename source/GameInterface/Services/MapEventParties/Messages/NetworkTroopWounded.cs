using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

[ProtoContract]
public readonly struct NetworkTroopWounded : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly string TroopId;

    public NetworkTroopWounded(string mapEventPartyId, string troopId)
    {
        MapEventPartyId = mapEventPartyId;
        TroopId = troopId;
    }
}