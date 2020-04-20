using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Network
{
    public class ByteReader
    {
        private readonly MemoryStream m_Stream;
        public readonly BinaryReader Binary;
        public long RemainingBytes
        {
            get
            {
                return m_Stream.Length - m_Stream.Position;
            }
        }
        public ByteReader(byte[] buffer)
        {
            m_Stream = new MemoryStream(buffer);
            Binary = new BinaryReader(m_Stream);
        }
        public ByteReader(MemoryStream stream)
        {
            m_Stream = stream;
            Binary = new BinaryReader(m_Stream);
        }

        public byte[] ToArray()
        {
            return m_Stream.ToArray();
        }
    }
}
