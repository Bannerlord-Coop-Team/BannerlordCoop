using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

[ProtoContract]
internal readonly struct NetworkMapEventFinalizeAttempted : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkMapEventFinalizeAttempted(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
