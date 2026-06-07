using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    public readonly struct ServerDisconnected : IEvent
    {
        public DisconnectInfo DisconnectInfo { get; }

        public ServerDisconnected(DisconnectInfo disconnectInfo)
        {
            DisconnectInfo = disconnectInfo;
        }
    }
}
