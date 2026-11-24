using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Coop.Core.Common.Network.Packets;

/// <summary>
/// Deflate compression for campaign saves sent during join.
/// </summary>
/// <remarks>
/// The raw save is tens of MB and LiteNetLib fragments it into MTU-sized reliable packets that drain
/// through a small fixed in-flight window — transfer time (and the joining peer's queue depth) scale
/// linearly with size, so compressing 2-4x shortens every join by the same factor.
/// <see cref="CompressionLevel.Fastest"/>: at these sizes deflate speed dwarfs the wire time saved by
/// a tighter ratio. Compression runs on the poll thread after the game-thread snapshot.
/// </remarks>
public static class SaveDataCompression
{
    internal static (Queue<byte[]> Chunks, int CompressedSize) CompressChunks(byte[] data, int chunkSize)
    {
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        using var output = new ChunkWriteStream(chunkSize);
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        return (output.Complete(), checked((int)output.Length));
    }

    public static byte[] Decompress(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Collects Deflate output directly into independently owned chunks.</summary>
    private sealed class ChunkWriteStream : Stream
    {
        private readonly int chunkSize;
        private readonly Queue<byte[]> chunks = new Queue<byte[]>();
        private byte[] chunk;
        private int chunkOffset;
        private long length;

        public ChunkWriteStream(int chunkSize)
        {
            this.chunkSize = chunkSize;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position
        {
            get => length;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            length += count;
            while (count > 0)
            {
                chunk ??= new byte[chunkSize];
                int copied = Math.Min(count, chunk.Length - chunkOffset);
                Buffer.BlockCopy(buffer, offset, chunk, chunkOffset, copied);
                offset += copied;
                count -= copied;
                chunkOffset += copied;

                if (chunkOffset == chunk.Length)
                {
                    chunks.Enqueue(chunk);
                    chunk = null;
                    chunkOffset = 0;
                }
            }
        }

        public Queue<byte[]> Complete()
        {
            if (chunk != null)
            {
                Array.Resize(ref chunk, chunkOffset);
                chunks.Enqueue(chunk);
                chunk = null;
            }
            if (chunks.Count == 0) chunks.Enqueue(Array.Empty<byte>());
            return chunks;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
