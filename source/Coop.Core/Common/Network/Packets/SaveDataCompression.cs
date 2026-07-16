using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// Compact forensic fingerprint of a transfer buffer (length, first/last bytes, content hash), logged
    /// on both ends of the save transfer so a join decode failure can be diagnosed by comparing the
    /// sender's and receiver's lines: identical fingerprints mean the payload arrived intact but was never
    /// the deflate stream this build's <see cref="Compress"/> produces (sender build mismatch); differing
    /// fingerprints mean the transfer or the packet envelope diverged in flight.
    /// </summary>
    public static string Describe(byte[] data)
    {
        if (data == null) return "<null>";
        if (data.Length == 0) return "<empty>";

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(data);

        int headLen = Math.Min(8, data.Length);
        return $"len={data.Length} head={ToHex(data, 0, headLen)} " +
               $"tail={ToHex(data, data.Length - headLen, headLen)} sha1={ToHex(hash, 0, 6)}";
    }

    private static string ToHex(byte[] data, int offset, int count)
    {
        var builder = new StringBuilder(count * 2);
        for (int i = offset; i < offset + count; i++)
            builder.Append(data[i].ToString("X2"));
        return builder.ToString();
    }
}
