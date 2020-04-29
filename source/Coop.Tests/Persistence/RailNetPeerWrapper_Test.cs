using Coop.Network;
using Moq;
using System;
using System.Linq;
using Coop.Multiplayer.Network;
using Xunit;

namespace Coop.Tests
{
    public class RailNetPeerWrapper_Test
    {
        private readonly Mock<INetworkConnection> m_NetworkConnection = TestUtils.CreateMockConnection();
        private readonly RailNetPeerWrapper m_Instance;
        private ArraySegment<byte> m_SendRawParam;
        private ArraySegment<byte> m_ReceiveParam;
        public RailNetPeerWrapper_Test()
        {
            m_NetworkConnection.Setup(con => con.SendRaw(It.IsAny<ArraySegment<byte>>())).Callback((ArraySegment<byte> arg) => m_SendRawParam = arg);
            m_Instance = new RailNetPeerWrapper(m_NetworkConnection.Object);
        }
        void Callback(ArraySegment<byte> buffer)
        {
            m_ReceiveParam = buffer;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(2048)]
        void SendRawPrependsPersistenceFlag(int iPayloadLength)
        {
            byte[] payload = Enumerable.Range(7, iPayloadLength).Select(i => (byte)i).ToArray();
            m_Instance.SendPayload(payload);

            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(PacketWriter.EncodePacketType(Protocol.EPacket.Persistence));
            writer.Binary.Write(payload);
            Assert.Equal(writer.ToArray(), m_SendRawParam);
        }
    }
}
