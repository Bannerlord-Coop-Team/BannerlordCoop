using System;
using System.IO;
using System.Linq;
using Moq;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;

namespace Coop.Tests
{
    public class ConnectionBase_Test
    {
        public ConnectionBase_Test()
        {
            m_GamePersistence = new Mock<IGameStatePersistence>();
            m_GamePersistence.Setup(per => per.Receive(It.IsAny<ArraySegment<byte>>()))
                             .Callback((ArraySegment<byte> arg) => m_ReceiveParam = arg);
            m_Connection = new Mock<ConnectionBase>(
                m_NetworkConnection.Object,
                m_GamePersistence.Object).Object;
        }

        private readonly Mock<INetworkConnection> m_NetworkConnection =
            TestUtils.CreateMockConnection();

        private readonly Mock<IGameStatePersistence> m_GamePersistence;
        private readonly ConnectionBase m_Connection;
        private ArraySegment<byte> m_ReceiveParam;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(2048)]
        public void ReceiveForPersistence(int iPayloadLength)
        {
            // Setup
            ArraySegment<byte> payload = TestUtils.MakePersistencePayload(iPayloadLength);

            // Receive
            m_Connection.Receive(payload);

            // Verify
            Assert.Equal(payload, m_ReceiveParam);
        }

        [Fact]
        public void DelegatesToSendRaw()
        {
            // Setup
            Packet packet = new Packet(EPacket.Client_Hello, new byte[100]);
            MemoryStream stream = new MemoryStream();
            new PacketWriter(packet).Write(new BinaryWriter(stream));

            // Send
            m_Connection.Send(packet);

            // Verify
            m_NetworkConnection.Verify(
                con => con.SendRaw(
                    It.Is<ArraySegment<byte>>(arg => arg.SequenceEqual(stream.ToArray())),
                    EDeliveryMethod.Reliable));
        }
    }
}
