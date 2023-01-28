using Common.Messaging;
using Common.Network;
using Coop.Tests.Stubs;
using LiteNetLib;

namespace Coop.Tests
{
    public class NetworkMessageBrokerStub : MessageBrokerStub, INetworkMessageBroker
    {
        public void PublishNetworkEvent(INetworkEvent networkEvent)
        {
            throw new System.NotImplementedException();
        }

        public void PublishNetworkEvent(INetworkEvent networkEvent, NetPeer peer)
        {
            throw new System.NotImplementedException();
        }
    }
}