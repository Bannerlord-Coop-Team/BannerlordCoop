using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    [ProtoContract]
    public readonly struct NetworkRequestSaveData : INetworkEvent
    {
    }
}
