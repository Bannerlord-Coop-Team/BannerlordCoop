using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages
{
    [ProtoContract]
    public readonly struct NetworkEnableTimeControls : INetworkEvent
    {
    }

    [ProtoContract]
    public readonly struct NetworkDisableTimeControls : INetworkEvent
    {
    }
}
