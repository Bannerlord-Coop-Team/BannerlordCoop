using System.IO;

namespace Coop.Network
{
    public class ByteWriter
    {
        public readonly BinaryWriter Binary;
        private readonly MemoryStream Stream;

        public ByteWriter()
        {
            Stream = new MemoryStream();
            Binary = new BinaryWriter(Stream);
        }

        public byte[] ToArray()
        {
            return Stream.ToArray();
        }
    }
}
