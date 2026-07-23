using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

[ProtoContract]
internal readonly struct NetworkMapEventFinalized : IEvent
{
}
