using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages
{
    [ProtoContract]
    public readonly struct NetworkPlayerMissionEntered : IEvent
    {
    }
}
