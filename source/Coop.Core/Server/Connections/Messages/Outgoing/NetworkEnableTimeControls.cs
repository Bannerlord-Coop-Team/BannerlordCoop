using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.States
{
    [ProtoContract]
    public readonly struct NetworkEnableTimeControls : INetworkEvent
    {
    }
}