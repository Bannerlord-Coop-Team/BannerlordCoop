using System.IO;

namespace Coop.Network
{
    public class ByteWriter
    {
        private readonly MemoryStream Stream;
        public readonly BinaryWriter Binary;
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
