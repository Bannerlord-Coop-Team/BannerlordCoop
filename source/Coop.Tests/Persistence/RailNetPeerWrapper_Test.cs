using System;
using System.Linq;
using Coop.NetImpl.LiteNet;
using Moq;
using Network;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;

namespace Coop.Tests.Persistence
{
    public class RailNetPeerWrapper_Test
    {
        public RailNetPeerWrapper_Test()
        {
            m_NetworkConnection
                .Setup(
                    con => con.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()))
                .Callback(
                    (ArraySegment<byte> arg, EDeliveryMethod eMethod) => m_SendRawParam = arg);
            m_Instance = new RailNetPeerWrapper(m_NetworkConnection.Object);
        }

        private readonly Mock<INetworkConnection> m_NetworkConnection =
            TestUtils.CreateMockConnection();

        private readonly RailNetPeerWrapper m_Instance;
        private ArraySegment<byte> m_SendRawParam;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(2048)]
        private void SendRawPrependsPersistenceFlag(int iPayloadLength)
        {
            byte[] payload = Enumerable.Range(7, iPayloadLength).Select(i => (byte) i).ToArray();
            m_Instance.SendPayload(payload);

            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(PacketWriter.EncodePacketType(EPacket.Persistence));
            writer.Binary.Write(payload);
            Assert.Equal(writer.ToArray(), m_SendRawParam);
        }

        [Fact]
        private void BufferOffsetIsRespected()
        {
            byte[] payload = Enumerable.Range(7, 64).Select(i => (byte) i).ToArray();
            int offset = 7;
            ArraySegment<byte> buffer = new ArraySegment<byte>(
                payload,
                offset,
                payload.Length - offset);
            m_Instance.SendPayload(buffer);

            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(PacketWriter.EncodePacketType(EPacket.Persistence));
            writer.Binary.Write(buffer);
            Assert.Equal(writer.ToArray(), m_SendRawParam);
        }
    }
}
