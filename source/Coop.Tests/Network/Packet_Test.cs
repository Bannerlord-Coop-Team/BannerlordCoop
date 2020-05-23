using System.IO;
using Network;
using Network.Protocol;
using Xunit;

namespace Coop.Tests
{
    public class Packet_Test
    {
        public Packet_Test()
        {
            m_Packet = new Packet(EPacket.Client_Hello, m_raw);
        }

        private readonly Packet m_Packet;

        private readonly byte[] m_raw =
        {
            0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf
        };

        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(100)]
        public void WriteReadFragmented(int iFragmentLength)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writerBinary = new BinaryWriter(stream);

            PacketWriter writer = new PacketWriter(m_Packet);
            while (!writer.Done)
            {
                writer.Write(writerBinary, iFragmentLength);
            }

            stream.Position = 0;
            ByteReader readerBinary = new ByteReader(stream);
            PacketReader reader = new PacketReader();
            Packet serializedPacket = null;
            while (!reader.Done)
            {
                serializedPacket = reader.Read(readerBinary);
            }

            Assert.NotNull(serializedPacket);
            Assert.Equal(m_Packet.Type, serializedPacket.Type);
            Assert.Equal(m_Packet.Payload, serializedPacket.Payload);
        }

        [Fact]
        public void ExceptionIfFragmentLengthIsTooSmall()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writerBinary = new BinaryWriter(stream);
            Assert.Throws<PacketSerializingException>(
                () => new PacketWriter(m_Packet).Write(writerBinary, Packet.MetaDataLength));
        }

        [Fact]
        public void InitializedAsNotDone()
        {
            Assert.False(new PacketReader().Done);
            Assert.False(new PacketWriter(m_Packet).Done);
        }

        [Fact]
        public void WriteReadWhole()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writerBinary = new BinaryWriter(stream);

            PacketWriter writer = new PacketWriter(m_Packet);
            writer.Write(writerBinary);
            Assert.True(writer.Done);

            stream.Position = 0;
            PacketReader reader = new PacketReader();
            Packet serializedPacket = reader.Read(new ByteReader(stream));

            Assert.True(reader.Done);
            Assert.NotNull(serializedPacket);
            Assert.Equal(m_Packet.Type, serializedPacket.Type);
            Assert.Equal(m_Packet.Payload, serializedPacket.Payload);
        }
    }
}
