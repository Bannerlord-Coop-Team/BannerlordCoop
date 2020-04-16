using System;
using System.IO;

namespace Coop.Network
{
    public class Packet
    {

        // 1 byte header + int32 payload length
        public const int MetaDataLength = 1 + 4;

        public const byte BitMaskType           = 0b00111111;
        public const byte BitMaskFragment_None  = 0b00000000;
        public const byte BitMaskFragment_More  = 0b01000000;
        public const byte BitMaskFragment_End   = 0b10000000;

        public Packet(Protocol.EPacket eType, byte[] payload)
        {
            Type = eType;
            Payload = payload;
        }
        public Protocol.EPacket Type
        {
            get; private set;
        }

        public int Length
        {
            get
            {
                return MetaDataLength + Payload.Length;
            }
        }

        public byte[] Payload;        
    }

    public class PacketWriter
    {
        public PacketWriter(Packet packet)
        {
            m_Packet = packet;
        }

        /// <summary>
        /// Writes the whole package to the provided writer.
        /// </summary>
        /// <param name="writer"></param>
        public void Write(BinaryWriter writer)
        {
            Write(writer, m_Packet.Payload.Length + Packet.MetaDataLength);
        }

        /// <summary>
        /// Writes at most <see cref="iNumberOfBytes"/> (including meta data) to the given 
        /// BinaryWriter. If the package length is longer than <see cref="iNumberOfBytes"/> 
        /// it will be fragmented.
        /// For fragmented packages, call this function until <see cref="Done"/> returns true.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="iNumberOfBytes">Maximum number of bytes that may be written.</param>
        public void Write(BinaryWriter writer, int iNumberOfBytes)
        {
            int iMinLength = Packet.MetaDataLength + Math.Min(m_Packet.Payload.Length, 1);
            if (iNumberOfBytes < iMinLength)
            {
                throw new PacketSerializingException($"Requested fragment size of {iNumberOfBytes} is too small to fit meta data & payload.");
            }

            if (Done)
            {
                throw new PacketSerializingException($"Package was already completely serialized to a writer.");
            }

            byte header;
            int iPayloadLength;
            if (m_iNumberOfWrittenPayloadBytes == 0)
            {
                if (m_Packet.Length <= iNumberOfBytes)
                {
                    // Single package
                    header = HeaderByte;
                    iPayloadLength = m_Packet.Payload.Length;
                }
                else
                {
                    // Start of a fragmented package
                    header = (byte)(HeaderByte | Packet.BitMaskFragment_More);
                    iPayloadLength = iNumberOfBytes - Packet.MetaDataLength;
                }
            }
            else
            {
                // Continuation of a fragmented package
                iPayloadLength = Math.Min(iNumberOfBytes - Packet.MetaDataLength, m_Packet.Payload.Length - m_iNumberOfWrittenPayloadBytes);
                bool bIsLastFragment = iPayloadLength + m_iNumberOfWrittenPayloadBytes >= m_Packet.Payload.Length;
                header = (byte)(HeaderByte | (bIsLastFragment ? Packet.BitMaskFragment_End : Packet.BitMaskFragment_More));
            }

            // Metadata
            writer.Write(header);
            writer.Write(iPayloadLength);

            // Payload
            writer.Write(m_Packet.Payload, m_iNumberOfWrittenPayloadBytes, iPayloadLength);
            m_iNumberOfWrittenPayloadBytes += iPayloadLength;

            Done = m_iNumberOfWrittenPayloadBytes >= m_Packet.Payload.Length;
        }

        /// <summary>
        /// Was the whole payload of the package serialized?
        /// </summary>
        public bool Done { get; private set; }

        private byte HeaderByte
        {
            get
            {
                return (byte)(Convert.ToByte(m_Packet.Type) & Packet.BitMaskType);
            }
        }

        private Packet m_Packet;
        private int m_iNumberOfWrittenPayloadBytes = 0;
    }

    public class PacketReader
    {
        /// <summary>
        /// Reads a <see cref="Packet"/> from the given reader. Returns null if the packet is fragmented and
        /// parts are still missing.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Serialized packet or null if the packet is fragmented and not yet complete.</returns>
        public Packet Read(ByteReader reader)
        {
            // 1 Byte header
            byte header = reader.Binary.ReadByte();
            Protocol.EPacket eType = (Protocol.EPacket)(header & Packet.BitMaskType);

            // 4 Bytes size of payload
            int iPayloadSize = reader.Binary.ReadInt32();

            // handle fragmented packages
            if((header & Packet.BitMaskFragment_More) != Packet.BitMaskFragment_None)
            {
                if(m_FragmentedBuffer == null)
                {
                    m_FragmentedStream = new MemoryStream();
                    m_FragmentedBuffer = new BinaryWriter(m_FragmentedStream);
                }

                m_FragmentedBuffer.Write(reader.Binary.ReadBytes(iPayloadSize));
                return null;
            }
            else if((header & Packet.BitMaskFragment_End) != Packet.BitMaskFragment_None)
            {
               if(m_FragmentedBuffer == null)
                {
                    throw new PacketSerializingException($"Package ({eType}) was flagged as END of a fragmented packet: got no START.");
                }

                m_FragmentedBuffer.Write(reader.Binary.ReadBytes(iPayloadSize));
                Done = true;
                return new Packet(eType, m_FragmentedStream.ToArray());
            }
            else
            {
                Done = true;
                return new Packet(eType, reader.Binary.ReadBytes(iPayloadSize));
            }
        }

        /// <summary>
        /// Was the whole payload of the package serialized?
        /// </summary>
        public bool Done
        {
            get; private set;
        }

        private MemoryStream m_FragmentedStream;
        private BinaryWriter m_FragmentedBuffer;
    }
}
