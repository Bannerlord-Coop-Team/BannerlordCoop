using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CoopMcpServer;

public interface IScreenshotImageEncoder
{
    EncodedScreenshot Encode(string path, string expectedSha256, CancellationToken cancellationToken = default);
}

public sealed record EncodedScreenshot(string Path, string Sha256, int Width, int Height, byte[] Png);

public sealed class ScreenshotImageEncoder : IScreenshotImageEncoder
{
    public const int MaximumBmpBytes = 64 * 1024 * 1024;
    public const int MaximumPngBytes = 8 * 1024 * 1024;
    public EncodedScreenshot Encode(string path, string expectedSha256, CancellationToken cancellationToken = default)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 54 || file.Length > MaximumBmpBytes) throw new IOException("BMP size exceeds 54..67108864 bytes.");
        byte[] bmp = new byte[(int)file.Length];
        for (int position = 0; position < bmp.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = file.Read(bmp, position, Math.Min(65536, bmp.Length - position));
            if (count == 0) throw new IOException("BMP was truncated during read.");
            position += count;
        }
        string hash = Convert.ToHexString(SHA256.HashData(bmp));
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("BMP changed after bridge stability evidence.");
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(offset, 4));
        int U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bmp.AsSpan(offset, 2));
        int width = I32(18), signedHeight = I32(22), bits = U16(28), offset = I32(10);
        if (bmp[0] != 'B' || bmp[1] != 'M' || I32(2) != bmp.Length || I32(14) < 40 ||
            width < 1 || width > 8192 || signedHeight == 0 || signedHeight < -8192 || signedHeight > 8192 ||
            U16(26) != 1 || (bits != 24 && bits != 32) || I32(30) != 0 || offset < 54 || offset < 14L + I32(14))
            throw new IOException("Only bounded uncompressed 24/32-bit BMP screenshots are supported.");
        int height = Math.Abs(signedHeight);
        if ((long)width * height > 16 * 1024 * 1024) throw new IOException("Screenshot exceeds 16 million pixels.");
        int stride = (((width * bits) + 31) / 32) * 4;
        if (offset + ((long)stride * height) > bmp.Length) throw new IOException("Truncated BMP pixels.");
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] row = new byte[1 + (width * 3)];
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int source = offset + ((signedHeight > 0 ? height - 1 - y : y) * stride);
                for (int x = 0; x < width; x++)
                {
                    row[1 + (x * 3)] = bmp[source + (x * (bits / 8)) + 2];
                    row[2 + (x * 3)] = bmp[source + (x * (bits / 8)) + 1];
                    row[3 + (x * 3)] = bmp[source + (x * (bits / 8))];
                }
                zlib.Write(row);
                if (compressed.Length > MaximumPngBytes) throw new IOException("PNG exceeds 8 MiB image limit.");
            }
        }
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; header[9] = 2;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        if (png.Length > MaximumPngBytes) throw new IOException("PNG exceeds 8 MiB image limit.");
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = png.ToArray();
        string pngPath = Path.ChangeExtension(path, ".png");
        using (var output = new FileStream(pngPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(bytes);
        return new EncodedScreenshot(pngPath, Convert.ToHexString(SHA256.HashData(bytes)), width, height, bytes);
    }

    private void WriteChunk(Stream stream, string type, byte[] payload)
    {
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        stream.Write(length);
        byte[] name = Encoding.ASCII.GetBytes(type);
        stream.Write(name); stream.Write(payload);
        uint crc = 0xffffffff;
        foreach (byte b in name.Concat(payload))
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320);
        }
        BinaryPrimitives.WriteUInt32BigEndian(length, ~crc);
        stream.Write(length);
    }
}
