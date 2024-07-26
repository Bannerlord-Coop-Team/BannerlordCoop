using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkDestroyMapEventSide(string MapEventSideId) : IEvent
{
    [ProtoMember(1)]
    public string MapEventSideId { get; } = MapEventSideId;
}