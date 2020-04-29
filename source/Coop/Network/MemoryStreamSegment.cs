using System;
using System.IO;
using JetBrains.Annotations;

namespace Coop.Network
{
    public class MemoryStreamSegment : MemoryStream
    {
        public MemoryStreamSegment([NotNull] ArraySegment<byte> buffer)
        : base(buffer.Array, buffer.Offset, buffer.Count)
        {

        }
    }
}
