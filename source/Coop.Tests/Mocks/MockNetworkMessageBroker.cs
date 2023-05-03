using Common.Messaging;
using Common.Network;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Coop.Tests.Mocks
{
    public class MockNetworkMessageBroker : MockMessageBroker, INetworkMessageBroker
    {
        public List<object> PublishedNetworkMessages { get; } = new List<object>();

        public void PublishNetworkEvent(INetworkEvent networkEvent)
        {
            PublishedNetworkMessages.Add(networkEvent);
        }

        public void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent)
        {
            PublishNetworkEvent(networkEvent);
        }
    }
}