using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

[ProtoContract]
internal readonly struct NetworkUpdateMapEventParty : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly FlattenedTroop[] FlattenedTroops;

    public NetworkUpdateMapEventParty(string mapEventPartyId, FlattenedTroop[] flattenedTroops)
    {
        MapEventPartyId = mapEventPartyId;
        FlattenedTroops = flattenedTroops;
    }
}
