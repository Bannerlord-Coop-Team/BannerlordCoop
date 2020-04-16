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
        private readonly MemoryStream Stream;
        public readonly BinaryReader Binary;
        public ByteReader(byte[] buffer)
        {
            Stream = new MemoryStream(buffer);
            Binary = new BinaryReader(Stream);
        }
        public ByteReader(MemoryStream stream)
        {
            Stream = stream;
            Binary = new BinaryReader(Stream);
        }

        public byte[] ToArray()
        {
            return Stream.ToArray();
        }
    }
}
