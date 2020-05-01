using System;
using System.IO;
using System.Linq;

namespace Coop.Network
{
    public class Packet
    {
        // 1 byte header + int32 payload length
        public const int MetaDataLength = 1 + 4;

        public const byte BitMaskType = 0b0011_1111;
        public const byte BitMaskFragment_None = 0b0000_0000;
        public const byte BitMaskFragment_More = 0b0100_0000;
        public const byte BitMaskFragment_End = 0b1000_0000;

        public ArraySegment<byte> Payload;

        private Packet(Protocol.EPacket eType, ArraySegment<byte> payload)
        {
            Type = eType;
            Payload = payload;
        }

        public Packet(Protocol.EPacket eType, byte[] payload) : this(
            eType,
            new ArraySegment<byte>(payload))
        {
        }

        public Packet(Protocol.EPacket eType, MemoryStream stream) : this(eType, stream.ToArray())
        {
        }

        public Protocol.EPacket Type { get; }

        public int Length => MetaDataLength + Payload.Count;
    }

    public class PacketWriter
    {
        private readonly Packet m_Packet;
        private int m_iNumberOfWrittenPayloadBytes;

        public PacketWriter(Packet packet)
        {
            m_Packet = packet;
        }

        /// <summary>
        ///     Was the whole payload of the package serialized?
        /// </summary>
        public bool Done { get; private set; }

        private byte HeaderByte => EncodePacketType(m_Packet.Type);

        public static byte EncodePacketType(Protocol.EPacket eType)
        {
            return (byte) (Convert.ToByte(eType) & Packet.BitMaskType);
        }

        /// <summary>
        ///     Writes the whole package to the provided writer.
        /// </summary>
        /// <param name="writer"></param>
        public void Write(BinaryWriter writer)
        {
            Write(writer, m_Packet.Length);
        }

        /// <summary>
        ///     Writes at most <see cref="iNumberOfBytes" /> (including meta data) to the given
        ///     BinaryWriter. If the package length is longer than <see cref="iNumberOfBytes" />
        ///     it will be fragmented.
        ///     For fragmented packages, call this function until <see cref="Done" /> returns true.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="iNumberOfBytes">Maximum number of bytes that may be written.</param>
        public void Write(BinaryWriter writer, int iNumberOfBytes)
        {
            int iMinLength = Packet.MetaDataLength + Math.Min(m_Packet.Payload.Count, 1);
            if (iNumberOfBytes < iMinLength)
            {
                throw new PacketSerializingException(
                    $"Requested fragment size of {iNumberOfBytes} is too small to fit meta data & payload.");
            }

            if (Done)
            {
                throw new PacketSerializingException(
                    "Package was already completely serialized to a writer.");
            }

            byte header;
            int iPayloadLength;
            if (m_iNumberOfWrittenPayloadBytes == 0)
            {
                if (m_Packet.Length <= iNumberOfBytes)
                {
                    // Single package
                    header = HeaderByte;
                    iPayloadLength = m_Packet.Payload.Count;
                }
                else
                {
                    // Start of a fragmented package
                    header = (byte) (HeaderByte | Packet.BitMaskFragment_More);
                    iPayloadLength = iNumberOfBytes - Packet.MetaDataLength;
                }
            }
            else
            {
                // Continuation of a fragmented package
                iPayloadLength = Math.Min(
                    iNumberOfBytes - Packet.MetaDataLength,
                    m_Packet.Payload.Count - m_iNumberOfWrittenPayloadBytes);
                bool bIsLastFragment = iPayloadLength + m_iNumberOfWrittenPayloadBytes >=
                                       m_Packet.Payload.Count;
                header = (byte) (HeaderByte |
                                 (bIsLastFragment ?
                                     Packet.BitMaskFragment_End :
                                     Packet.BitMaskFragment_More));
            }

            // Metadata
            writer.Write(header);
            writer.Write(iPayloadLength);

            // Payload
            writer.Write(
                m_Packet.Payload.ToArray(),
                m_iNumberOfWrittenPayloadBytes,
                iPayloadLength);
            m_iNumberOfWrittenPayloadBytes += iPayloadLength;

            Done = m_iNumberOfWrittenPayloadBytes >= m_Packet.Payload.Count;
        }
    }

    public class PacketReader
    {
        private BinaryWriter m_FragmentedBuffer;

        private MemoryStream m_FragmentedStream;

        /// <summary>
        ///     Was the whole payload of the package serialized?
        /// </summary>
        public bool Done { get; private set; }

        public static Protocol.EPacket DecodePacketType(byte header)
        {
            return (Protocol.EPacket) (header & Packet.BitMaskType);
        }

        /// <summary>
        ///     Reads a <see cref="Packet" /> from the given reader. Returns null if the packet is fragmented and
        ///     parts are still missing.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Serialized packet or null if the packet is fragmented and not yet complete.</returns>
        public Packet Read(ByteReader reader)
        {
            // 1 Byte header
            byte header = reader.Binary.ReadByte();
            Protocol.EPacket eType = DecodePacketType(header);

            // 4 Bytes size of payload
            int iPayloadSize = reader.Binary.ReadInt32();

            // handle fragmented packages
            if ((header & Packet.BitMaskFragment_More) != Packet.BitMaskFragment_None)
            {
                if (m_FragmentedBuffer == null)
                {
                    m_FragmentedStream = new MemoryStream();
                    m_FragmentedBuffer = new BinaryWriter(m_FragmentedStream);
                }

                m_FragmentedBuffer.Write(reader.Binary.ReadBytes(iPayloadSize));
                return null;
            }

            if ((header & Packet.BitMaskFragment_End) != Packet.BitMaskFragment_None)
            {
                if (m_FragmentedBuffer == null)
                {
                    throw new PacketSerializingException(
                        $"Package ({eType}) was flagged as END of a fragmented packet: got no START.");
                }

                m_FragmentedBuffer.Write(reader.Binary.ReadBytes(iPayloadSize));
                Done = true;
                return new Packet(eType, m_FragmentedStream);
            }

            Done = true;
            return new Packet(eType, reader.Binary.ReadBytes(iPayloadSize));
        }
    }
}
