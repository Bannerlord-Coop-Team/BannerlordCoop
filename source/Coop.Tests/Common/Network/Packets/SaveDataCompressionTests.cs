using Coop.Core.Common.Network.Packets;
using System.IO;
using Xunit;

namespace Coop.Tests.Network.Packets;

public class SaveDataCompressionTests
{
    [Fact]
    public void Decompress_ExpandedDataOverLimit_IsRejected()
    {
        byte[] compressed = SaveDataCompression.Compress(new byte[1024]);

        Assert.Throws<InvalidDataException>(() =>
            SaveDataCompression.Decompress(compressed, compressed.Length, 512));
    }

    [Fact]
    public void Decompress_CompressedDataOverLimit_IsRejectedBeforeDecompression()
    {
        byte[] compressed = SaveDataCompression.Compress(new byte[1024]);

        Assert.Throws<InvalidDataException>(() =>
            SaveDataCompression.Decompress(compressed, compressed.Length - 1, 2048));
    }
}
