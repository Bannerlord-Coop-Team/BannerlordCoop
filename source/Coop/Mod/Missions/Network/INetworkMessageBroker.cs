using Common.Messaging;
using LiteNetLib;


namespace Coop.Mod.Missions.Network
{
    public interface INetworkMessageBroker : IMessageBroker
    {
        void Publish<T>(T message, NetPeer peer = null);
    }
}