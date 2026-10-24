using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

[ProtoContract]
internal readonly struct NetworkMapEventFinalized : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkMapEventFinalized(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
