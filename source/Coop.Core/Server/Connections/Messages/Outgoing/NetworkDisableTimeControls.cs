using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages.Outgoing
{
    [ProtoContract]
    public readonly struct NetworkDisableTimeControls : INetworkEvent
    {
    }
}
