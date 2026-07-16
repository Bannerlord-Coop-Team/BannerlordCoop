using System.IO;
using System.IO.Compression;
using System;

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
    public const int MaxCompressedBytes = 128 * 1024 * 1024;
    public const int MaxDecompressedBytes = 256 * 1024 * 1024;

    public static byte[] Compress(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length > MaxDecompressedBytes)
            throw new InvalidDataException($"Save data exceeded {MaxDecompressedBytes} bytes");

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest))
        {
            deflate.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public static byte[] Decompress(byte[] compressedData)
    {
        return Decompress(compressedData, MaxCompressedBytes, MaxDecompressedBytes);
    }

    internal static byte[] Decompress(
        byte[] compressedData,
        int maxCompressedBytes,
        int maxDecompressedBytes)
    {
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));
        if (maxCompressedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxCompressedBytes));
        if (maxDecompressedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxDecompressedBytes));
        if (compressedData.Length == 0 || compressedData.Length > maxCompressedBytes)
            throw new InvalidDataException(
                $"Compressed save size must be between 1 and {maxCompressedBytes} bytes");

        using var input = new MemoryStream(compressedData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        var buffer = new byte[81920];
        int totalBytes = 0;
        int bytesRead;
        while ((bytesRead = deflate.Read(buffer, 0, buffer.Length)) != 0)
        {
            totalBytes = checked(totalBytes + bytesRead);
            if (totalBytes > maxDecompressedBytes)
                throw new InvalidDataException($"Decompressed save exceeded {maxDecompressedBytes} bytes");

            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }
}
