using Common.Messaging;
using LiteNetLib;


namespace Missions.Network
{
    public interface INetworkMessageBroker : IMessageBroker
    {
        void Publish<T>(T message, NetPeer peer = null);
    }
}