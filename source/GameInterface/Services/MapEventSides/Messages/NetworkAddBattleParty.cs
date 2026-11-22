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
    [ProtoMember(3)]
    public readonly string PartyId;

    public NetworkAddBattleParty(string mapEventSideId, string mapEventPartyId, string partyId)
    {
        MapEventSideId = mapEventSideId;
        MapEventPartyId = mapEventPartyId;
        PartyId = partyId;
    }
}
