using System;
using System.IO;

namespace Coop.Network
{
    public class MemoryStreamSegment : MemoryStream
    {
        public MemoryStreamSegment(ArraySegment<byte> buffer) : base(
            buffer.Array,
            buffer.Offset,
            buffer.Count)
        {
        }
    }
}
