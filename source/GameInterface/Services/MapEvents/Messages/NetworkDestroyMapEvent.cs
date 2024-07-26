using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkDestroyMapEvent(string MapEventId) : ICommand
{
    [ProtoMember(1)]
    public string MapEventId { get; } = MapEventId;
}