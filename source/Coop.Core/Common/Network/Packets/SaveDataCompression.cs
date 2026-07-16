using System.IO;
using System.IO.Compression;

namespace Coop.Core.Common.Network.Packets;

/// <summary>
/// Deflate compression for the transfer save payload of <see cref="GameSaveDataPacket"/>.
/// </summary>
/// <remarks>
/// The raw save is tens of MB and LiteNetLib fragments it into MTU-sized reliable packets that drain
/// through a small fixed in-flight window — transfer time (and the joining peer's queue depth) scale
/// linearly with size, so compressing 2-4x shortens every join by the same factor.
/// <see cref="CompressionLevel.Fastest"/>: at these sizes deflate speed dwarfs the wire time saved by
/// a tighter ratio, and the compression runs during the join's blocking save snapshot.
/// </remarks>
public static class SaveDataCompression
{
    public static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest))
        {
            deflate.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public static byte[] Decompress(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }
}
