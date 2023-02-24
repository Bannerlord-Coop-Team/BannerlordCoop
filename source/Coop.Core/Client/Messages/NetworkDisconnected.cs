using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Client.Messages
{
    public readonly struct NetworkDisconnected : IEvent
    {
        public DisconnectInfo DisconnectInfo { get; }

        public NetworkDisconnected(DisconnectInfo disconnectInfo)
        {
            DisconnectInfo = disconnectInfo;
        }
    }
}
