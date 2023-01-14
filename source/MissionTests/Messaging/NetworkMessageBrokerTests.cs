using Common.Messaging;
using LiteNetLib;
using Missions.Services.Messaging;
using Missions.Services.Network;
using Moq;
using ProtoBuf;
using System;
using Xunit;

namespace MissionTests.Messaging
{
    public class NetworkMessageBrokerTests
    {
        [Fact]
        public void NetworkMessageBroker_Construct()
        {
            var client = new Mock<INetworkClient>();

            var messageBroker = new NetworkMessageBroker(client.Object);

            Assert.NotNull(messageBroker);
        }

        [Fact]
        public void NetworkMessageBroker_PublishEvent()
        {
            var client = new Mock<INetworkClient>();

            int publishCounter = 0;

            client.Setup((m) => m.Send(It.IsAny<IPacket>(), It.IsAny<NetPeer>())).Callback(() => publishCounter++);


            var messageBroker = new NetworkMessageBroker(client.Object);

            Assert.Equal(0, publishCounter);

            messageBroker.PublishEvent(new TestEvent(), null);

            Assert.Equal(1, publishCounter);
        }

        [Fact]
        public void NetworkMessageBroker_PublishAllEvent()
        {
            var client = new Mock<INetworkClient>();

            int publishCounter = 0;

            client.Setup((m) => m.SendAll(It.IsAny<IPacket>())).Callback(() => publishCounter++);

            var messageBroker = new NetworkMessageBroker(client.Object);

            Assert.Equal(0, publishCounter);

            messageBroker.PublishAllEvent(new TestEvent());

            Assert.Equal(1, publishCounter);
        }
    }

    [ProtoContract]
    class TestEvent : INetworkEvent
    {

    }
}
