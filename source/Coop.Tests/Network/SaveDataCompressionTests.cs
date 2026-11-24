using Coop.Core.Common.Network.Packets;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.Network;

/// <summary>Checks that compressed writes split cleanly across chunk boundaries.</summary>
public class SaveDataCompressionTests
{
    [Fact]
    public void CompressChunks_RoundTripsWritesThatCrossChunkBoundaries()
    {
        // Deflate can write in 8 KiB blocks. A 1,000-byte chunk forces a split within a write.
        const int chunkSize = 1000;
        var data = new byte[16 * 1024];
        new Random(42).NextBytes(data);

        var (chunks, compressedSize) = SaveDataCompression.CompressChunks(data, chunkSize);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks.SkipLast(1), chunk => Assert.Equal(chunkSize, chunk.Length));
        Assert.InRange(chunks.Last().Length, 1, chunkSize);
        byte[] compressed = chunks.SelectMany(chunk => chunk).ToArray();
        Assert.Equal(compressed.Length, compressedSize);
        Assert.Equal(data, SaveDataCompression.Decompress(compressed));
    }
}
