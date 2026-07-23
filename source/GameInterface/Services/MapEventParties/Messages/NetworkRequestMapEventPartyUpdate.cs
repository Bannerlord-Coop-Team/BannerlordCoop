using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

[ProtoContract]
internal readonly struct NetworkRequestMapEventPartyUpdate : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;

    public NetworkRequestMapEventPartyUpdate(string mapEventPartyId)
    {
        MapEventPartyId = mapEventPartyId;
    }
}
