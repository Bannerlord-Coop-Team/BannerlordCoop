using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventSides.Messages;

[ProtoContract]
public readonly struct NetworkAddBattleParty : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventSideId;
    [ProtoMember(2)]
    public readonly string MapEventPartyId;

    public NetworkAddBattleParty(string mapEventSideId, string mapEventPartyId)
    {
        MapEventSideId = mapEventSideId;
        MapEventPartyId = mapEventPartyId;
    }
}
