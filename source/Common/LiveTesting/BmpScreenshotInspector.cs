using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Common.LiveTesting;

public interface IBmpScreenshotInspector
{
    BmpScreenshotObservation ObserveFile(string path);

    bool TryInspectStableFile(
        string path,
        BmpScreenshotObservation expectedObservation,
        out BmpScreenshotEvidence evidence);

    BmpScreenshotEvidence Inspect(byte[] bytes);
}

public sealed class BmpScreenshotInspector : IBmpScreenshotInspector
{
    private const int MinimumHeaderLength = 54;

    public BmpScreenshotObservation ObserveFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
        if (!File.Exists(path)) return BmpScreenshotObservation.Missing;

        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = stream.Length;
                var header = new byte[(int)Math.Min(MinimumHeaderLength, length)];
                int bytesRead = ReadToEnd(stream, header);
                if (bytesRead != header.Length)
                {
                    return BmpScreenshotObservation.Unreadable(length, File.GetLastWriteTimeUtc(path));
                }

                BmpHeader bmpHeader = ParseHeader(header, length);
                return new BmpScreenshotObservation(
                    true,
                    bmpHeader.IsValid,
                    length,
                    bmpHeader.DeclaredLength,
                    bmpHeader.Width,
                    bmpHeader.Height,
                    bmpHeader.BitsPerPixel,
                    File.GetLastWriteTimeUtc(path));
            }
        }
        catch (IOException)
        {
            return BmpScreenshotObservation.Unreadable(0, null);
        }
        catch (UnauthorizedAccessException)
        {
            return BmpScreenshotObservation.Unreadable(0, null);
        }
    }

    public bool TryInspectStableFile(
        string path,
        BmpScreenshotObservation expectedObservation,
        out BmpScreenshotEvidence evidence)
    {
        evidence = null;
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
        if (expectedObservation == null) throw new ArgumentNullException(nameof(expectedObservation));
        if (!expectedObservation.Exists ||
            !expectedObservation.HeaderValid ||
            !expectedObservation.LengthMatchesHeader ||
            !expectedObservation.LastWriteUtc.HasValue ||
            expectedObservation.Length <= 0 ||
            expectedObservation.Length > int.MaxValue)
        {
            return false;
        }

        try
        {
            var before = new FileInfo(path);
            before.Refresh();
            if (!Matches(expectedObservation, before)) return false;

            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != expectedObservation.Length) return false;

                bytes = new byte[(int)stream.Length];
                if (ReadToEnd(stream, bytes) != bytes.Length) return false;
                if (stream.Length != expectedObservation.Length) return false;
            }

            var after = new FileInfo(path);
            after.Refresh();
            if (!Matches(expectedObservation, after)) return false;

            BmpScreenshotEvidence inspection = Inspect(bytes);
            if (!inspection.HeaderValid ||
                inspection.Length != expectedObservation.Length ||
                inspection.DeclaredLength != expectedObservation.DeclaredLength)
            {
                return false;
            }

            evidence = inspection;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public BmpScreenshotEvidence Inspect(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));

        BmpHeader header = ParseHeader(bytes, bytes.LongLength);
        if (!header.IsValid)
        {
            return BmpScreenshotEvidence.Rejected(
                bytes.LongLength,
                header,
                bytes.Length == 0
                    ? BmpScreenshotQualityVerdict.Empty
                    : BmpScreenshotQualityVerdict.Malformed,
                bytes.Length == 0
                    ? "The screenshot file is empty."
                    : "The BMP header or pixel layout is invalid.");
        }

        PixelRanges ranges = ReadPixelRanges(bytes, header);
        BmpScreenshotQualityVerdict verdict;
        string reason;
        if (ranges.MaximumRed <= 3 && ranges.MaximumGreen <= 3 && ranges.MaximumBlue <= 3)
        {
            verdict = BmpScreenshotQualityVerdict.AllBlack;
            reason = "Every RGB channel is at or below 3.";
        }
        else if (ranges.MinimumRed >= 252 && ranges.MinimumGreen >= 252 && ranges.MinimumBlue >= 252)
        {
            verdict = BmpScreenshotQualityVerdict.AllWhite;
            reason = "Every RGB channel is at or above 252.";
        }
        else if (ranges.RedRange <= 4 && ranges.GreenRange <= 4 && ranges.BlueRange <= 4)
        {
            verdict = BmpScreenshotQualityVerdict.NearUniform;
            reason = "Every RGB channel varies by at most 4 across the frame.";
        }
        else
        {
            verdict = BmpScreenshotQualityVerdict.NonUniformPixelData;
            reason = "The BMP contains non-uniform pixel data; semantic visual correctness was not evaluated.";
        }

        string sha256;
        using (SHA256 algorithm = SHA256.Create())
        {
            sha256 = ToLowerHexadecimal(algorithm.ComputeHash(bytes));
        }

        return new BmpScreenshotEvidence(
            bytes.LongLength,
            header.DeclaredLength,
            true,
            header.Width,
            header.Height,
            header.BitsPerPixel,
            sha256,
            verdict,
            reason);
    }

    private static bool Matches(BmpScreenshotObservation expected, FileInfo actual)
    {
        return actual.Exists &&
            actual.Length == expected.Length &&
            actual.LastWriteTimeUtc == expected.LastWriteUtc.Value;
    }

    private static int ReadToEnd(Stream stream, byte[] bytes)
    {
        int offset = 0;
        while (offset < bytes.Length)
        {
            int bytesRead = stream.Read(bytes, offset, bytes.Length - offset);
            if (bytesRead == 0) break;
            offset += bytesRead;
        }

        return offset;
    }

    private static BmpHeader ParseHeader(byte[] bytes, long actualLength)
    {
        if (bytes.Length < MinimumHeaderLength ||
            bytes[0] != (byte)'B' ||
            bytes[1] != (byte)'M')
        {
            return BmpHeader.Invalid;
        }

        uint declaredLength = ReadUInt32(bytes, 2);
        uint pixelOffset = ReadUInt32(bytes, 10);
        uint dibHeaderLength = ReadUInt32(bytes, 14);
        int width = ReadInt32(bytes, 18);
        int signedHeight = ReadInt32(bytes, 22);
        ushort planes = ReadUInt16(bytes, 26);
        ushort bitsPerPixel = ReadUInt16(bytes, 28);
        uint compression = ReadUInt32(bytes, 30);

        if (declaredLength != actualLength ||
            dibHeaderLength < 40 ||
            14L + dibHeaderLength > pixelOffset ||
            pixelOffset > actualLength ||
            width <= 0 ||
            signedHeight == 0 ||
            signedHeight == int.MinValue ||
            planes != 1 ||
            (bitsPerPixel != 24 && bitsPerPixel != 32) ||
            compression != 0)
        {
            return new BmpHeader(false, declaredLength, width, 0, bitsPerPixel, pixelOffset, 0);
        }

        int height = Math.Abs(signedHeight);
        long rowLength = ((((long)width * bitsPerPixel) + 31L) / 32L) * 4L;
        long availablePixelBytes = actualLength - pixelOffset;
        if (rowLength <= 0 || availablePixelBytes <= 0 || rowLength > availablePixelBytes / height)
        {
            return new BmpHeader(false, declaredLength, width, height, bitsPerPixel, pixelOffset, rowLength);
        }

        return new BmpHeader(true, declaredLength, width, height, bitsPerPixel, pixelOffset, rowLength);
    }

    private static PixelRanges ReadPixelRanges(byte[] bytes, BmpHeader header)
    {
        int bitsPerPixel = header.BitsPerPixel.Value;
        int height = header.Height.Value;
        int width = header.Width.Value;
        int bytesPerPixel = bitsPerPixel / 8;
        int minimumRed = byte.MaxValue;
        int minimumGreen = byte.MaxValue;
        int minimumBlue = byte.MaxValue;
        int maximumRed = byte.MinValue;
        int maximumGreen = byte.MinValue;
        int maximumBlue = byte.MinValue;

        for (int row = 0; row < height; row++)
        {
            long rowOffset = header.PixelOffset + (row * header.RowLength);
            for (int column = 0; column < width; column++)
            {
                int offset = checked((int)(rowOffset + ((long)column * bytesPerPixel)));
                int blue = bytes[offset];
                int green = bytes[offset + 1];
                int red = bytes[offset + 2];
                minimumRed = Math.Min(minimumRed, red);
                minimumGreen = Math.Min(minimumGreen, green);
                minimumBlue = Math.Min(minimumBlue, blue);
                maximumRed = Math.Max(maximumRed, red);
                maximumGreen = Math.Max(maximumGreen, green);
                maximumBlue = Math.Max(maximumBlue, blue);
            }
        }

        return new PixelRanges(
            minimumRed,
            minimumGreen,
            minimumBlue,
            maximumRed,
            maximumGreen,
            maximumBlue);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return (uint)(bytes[offset] |
            (bytes[offset + 1] << 8) |
            (bytes[offset + 2] << 16) |
            (bytes[offset + 3] << 24));
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return unchecked((int)ReadUInt32(bytes, offset));
    }

    private static string ToLowerHexadecimal(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }

    internal readonly struct BmpHeader
    {
        public static readonly BmpHeader Invalid = new BmpHeader(false, null, null, null, null, 0, 0);

        public bool IsValid { get; }
        public long? DeclaredLength { get; }
        public int? Width { get; }
        public int? Height { get; }
        public ushort? BitsPerPixel { get; }
        public long PixelOffset { get; }
        public long RowLength { get; }

        public BmpHeader(
            bool isValid,
            long? declaredLength,
            int? width,
            int? height,
            ushort? bitsPerPixel,
            long pixelOffset,
            long rowLength)
        {
            IsValid = isValid;
            DeclaredLength = declaredLength;
            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            PixelOffset = pixelOffset;
            RowLength = rowLength;
        }
    }

    private readonly struct PixelRanges
    {
        public int MinimumRed { get; }
        public int MinimumGreen { get; }
        public int MinimumBlue { get; }
        public int MaximumRed { get; }
        public int MaximumGreen { get; }
        public int MaximumBlue { get; }
        public int RedRange => MaximumRed - MinimumRed;
        public int GreenRange => MaximumGreen - MinimumGreen;
        public int BlueRange => MaximumBlue - MinimumBlue;

        public PixelRanges(
            int minimumRed,
            int minimumGreen,
            int minimumBlue,
            int maximumRed,
            int maximumGreen,
            int maximumBlue)
        {
            MinimumRed = minimumRed;
            MinimumGreen = minimumGreen;
            MinimumBlue = minimumBlue;
            MaximumRed = maximumRed;
            MaximumGreen = maximumGreen;
            MaximumBlue = maximumBlue;
        }
    }
}

public sealed class BmpScreenshotObservation
{
    public static readonly BmpScreenshotObservation Missing =
        new BmpScreenshotObservation(false, false, 0, null, null, null, null, null);

    public bool Exists { get; }
    public bool HeaderValid { get; }
    public long Length { get; }
    public long? DeclaredLength { get; }
    public bool LengthMatchesHeader => DeclaredLength == Length;
    public int? Width { get; }
    public int? Height { get; }
    public ushort? BitsPerPixel { get; }
    public DateTime? LastWriteUtc { get; }

    public BmpScreenshotObservation(
        bool exists,
        bool headerValid,
        long length,
        long? declaredLength,
        int? width,
        int? height,
        ushort? bitsPerPixel,
        DateTime? lastWriteUtc)
    {
        Exists = exists;
        HeaderValid = headerValid;
        Length = length;
        DeclaredLength = declaredLength;
        Width = width;
        Height = height;
        BitsPerPixel = bitsPerPixel;
        LastWriteUtc = lastWriteUtc;
    }

    public bool IsFreshFor(DateTime captureRequestedUtc)
    {
        return LastWriteUtc.HasValue && LastWriteUtc.Value >= captureRequestedUtc;
    }

    internal static BmpScreenshotObservation Unreadable(long length, DateTime? lastWriteUtc)
    {
        return new BmpScreenshotObservation(true, false, length, null, null, null, null, lastWriteUtc);
    }
}

public sealed class BmpScreenshotEvidence
{
    public long Length { get; }
    public long? DeclaredLength { get; }
    public bool HeaderValid { get; }
    public int? Width { get; }
    public int? Height { get; }
    public ushort? BitsPerPixel { get; }
    public string Sha256 { get; }
    public BmpScreenshotQualityVerdict QualityVerdict { get; }
    public string QualityReason { get; }
    public bool PassesBasicQuality => QualityVerdict == BmpScreenshotQualityVerdict.NonUniformPixelData;

    public BmpScreenshotEvidence(
        long length,
        long? declaredLength,
        bool headerValid,
        int? width,
        int? height,
        ushort? bitsPerPixel,
        string sha256,
        BmpScreenshotQualityVerdict qualityVerdict,
        string qualityReason)
    {
        Length = length;
        DeclaredLength = declaredLength;
        HeaderValid = headerValid;
        Width = width;
        Height = height;
        BitsPerPixel = bitsPerPixel;
        Sha256 = sha256;
        QualityVerdict = qualityVerdict;
        QualityReason = qualityReason;
    }

    internal static BmpScreenshotEvidence Rejected(
        long length,
        BmpScreenshotInspector.BmpHeader header,
        BmpScreenshotQualityVerdict verdict,
        string reason)
    {
        return new BmpScreenshotEvidence(
            length,
            header.DeclaredLength,
            false,
            header.Width,
            header.Height,
            header.BitsPerPixel,
            null,
            verdict,
            reason);
    }
}

public enum BmpScreenshotQualityVerdict
{
    Empty,
    Malformed,
    AllBlack,
    AllWhite,
    NearUniform,
    NonUniformPixelData,
}
