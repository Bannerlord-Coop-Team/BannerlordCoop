using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;
using Moq;
using ProtoBuf;

namespace Common.Tests.Messaging
{
    public class NetworkMessageBrokerTests
    {
        [Fact]
        public void NetworkMessageBroker_Construct()
        {
            var client = new Mock<INetwork>();

            var messageBroker = new NetworkMessageBroker
            {
                Network = client.Object
            };

            Assert.NotNull(messageBroker);
        }

        [Fact]
        public void NetworkMessageBroker_PublishEvent()
        {
            var client = new Mock<INetwork>();

            int publishCounter = 0;

            client.Setup((m) => m.Send(It.IsAny<NetPeer>(), It.IsAny<IPacket>())).Callback(() => publishCounter++);


            var messageBroker = new NetworkMessageBroker
            {
                Network = client.Object
            };

            Assert.Equal(0, publishCounter);

            messageBroker.PublishNetworkEvent(null, new TestEvent());

            Assert.Equal(1, publishCounter);
        }

        [Fact]
        public void NetworkMessageBroker_PublishAllEvent()
        {
            var client = new Mock<INetwork>();

            int publishCounter = 0;

            client.Setup((m) => m.SendAll(It.IsAny<IPacket>())).Callback(() => publishCounter++);

            var messageBroker = new NetworkMessageBroker
            {
                Network = client.Object
            };

            Assert.Equal(0, publishCounter);

            messageBroker.PublishNetworkEvent(new TestEvent());

            Assert.Equal(1, publishCounter);
        }
    }

    [ProtoContract]
    class TestEvent : INetworkEvent
    {

    }
}
