using Common.LiveTesting;

namespace Common.Tests.LiveTesting;

public class BmpScreenshotInspectorTests
{
    private readonly BmpScreenshotInspector inspector = new BmpScreenshotInspector();

    [Fact]
    public void Inspect_NonUniform24BitBmp_ReportsDimensionsAndBasicQuality()
    {
        byte[] bmp = Create24BitBmp(
            2,
            2,
            new[]
            {
                Rgb(0, 0, 0),
                Rgb(255, 255, 255),
                Rgb(255, 0, 0),
                Rgb(0, 0, 255),
            });

        BmpScreenshotEvidence evidence = inspector.Inspect(bmp);

        Assert.True(evidence.HeaderValid);
        Assert.Equal(bmp.Length, evidence.DeclaredLength);
        Assert.Equal(2, evidence.Width);
        Assert.Equal(2, evidence.Height);
        Assert.Equal((ushort)24, evidence.BitsPerPixel);
        Assert.Equal(64, evidence.Sha256.Length);
        Assert.Equal(BmpScreenshotQualityVerdict.NonUniformPixelData, evidence.QualityVerdict);
        Assert.True(evidence.PassesBasicQuality);
        Assert.Contains("semantic visual correctness was not evaluated", evidence.QualityReason);
    }

    [Theory]
    [InlineData(0, 0, 0, BmpScreenshotQualityVerdict.AllBlack)]
    [InlineData(255, 255, 255, BmpScreenshotQualityVerdict.AllWhite)]
    public void Inspect_SolidExtremeFrame_IsRejected(
        byte red,
        byte green,
        byte blue,
        BmpScreenshotQualityVerdict expectedVerdict)
    {
        byte[] bmp = Create24BitBmp(
            2,
            2,
            Enumerable.Repeat(Rgb(red, green, blue), 4).ToArray());

        BmpScreenshotEvidence evidence = inspector.Inspect(bmp);

        Assert.Equal(expectedVerdict, evidence.QualityVerdict);
        Assert.False(evidence.PassesBasicQuality);
    }

    [Fact]
    public void Inspect_NearUniformFrame_IsRejected()
    {
        byte[] bmp = Create24BitBmp(
            2,
            2,
            new[]
            {
                Rgb(100, 101, 102),
                Rgb(101, 102, 103),
                Rgb(102, 103, 104),
                Rgb(104, 104, 104),
            });

        BmpScreenshotEvidence evidence = inspector.Inspect(bmp);

        Assert.Equal(BmpScreenshotQualityVerdict.NearUniform, evidence.QualityVerdict);
        Assert.False(evidence.PassesBasicQuality);
    }

    [Fact]
    public void Inspect_EmptyOrMalformedBmp_IsRejectedWithoutHash()
    {
        BmpScreenshotEvidence empty = inspector.Inspect(Array.Empty<byte>());
        byte[] malformed = Create24BitBmp(1, 1, new[] { Rgb(10, 20, 30) });
        malformed[0] = (byte)'N';

        BmpScreenshotEvidence invalid = inspector.Inspect(malformed);

        Assert.Equal(BmpScreenshotQualityVerdict.Empty, empty.QualityVerdict);
        Assert.Equal(BmpScreenshotQualityVerdict.Malformed, invalid.QualityVerdict);
        Assert.False(empty.HeaderValid);
        Assert.False(invalid.HeaderValid);
        Assert.Null(empty.Sha256);
        Assert.Null(invalid.Sha256);
    }

    [Fact]
    public void ObserveFile_ReportsFreshnessAndFinalizesAnUnchangedFile()
    {
        string path = NewTemporaryPath();
        try
        {
            byte[] bmp = Create24BitBmp(
                2,
                1,
                new[] { Rgb(5, 10, 15), Rgb(200, 210, 220) });
            File.WriteAllBytes(path, bmp);
            DateTime writtenUtc = DateTime.UtcNow.AddMinutes(-2);
            File.SetLastWriteTimeUtc(path, writtenUtc);

            BmpScreenshotObservation observation = inspector.ObserveFile(path);

            Assert.True(observation.HeaderValid);
            Assert.False(observation.IsFreshFor(writtenUtc.AddMinutes(1)));
            Assert.True(observation.IsFreshFor(writtenUtc.AddMinutes(-1)));
            Assert.True(inspector.TryInspectStableFile(path, observation, out var evidence));
            Assert.Equal(BmpScreenshotQualityVerdict.NonUniformPixelData, evidence.QualityVerdict);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryInspectStableFile_RejectsAFileChangedAfterObservation()
    {
        string path = NewTemporaryPath();
        try
        {
            byte[] first = Create24BitBmp(
                2,
                1,
                new[] { Rgb(10, 20, 30), Rgb(200, 210, 220) });
            byte[] second = Create24BitBmp(
                2,
                1,
                new[] { Rgb(30, 20, 10), Rgb(220, 210, 200) });
            File.WriteAllBytes(path, first);
            DateTime firstWriteUtc = DateTime.UtcNow.AddMinutes(-2);
            File.SetLastWriteTimeUtc(path, firstWriteUtc);
            BmpScreenshotObservation observation = inspector.ObserveFile(path);

            File.WriteAllBytes(path, second);
            File.SetLastWriteTimeUtc(path, firstWriteUtc.AddMinutes(1));

            Assert.False(inspector.TryInspectStableFile(path, observation, out var evidence));
            Assert.Null(evidence);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Create24BitBmp(int width, int height, RgbColor[] pixels)
    {
        Assert.Equal(width * height, pixels.Length);
        int rowLength = ((width * 3) + 3) & ~3;
        int pixelLength = rowLength * height;
        int fileLength = 54 + pixelLength;
        var bytes = new byte[fileLength];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        WriteUInt32(bytes, 2, (uint)fileLength);
        WriteUInt32(bytes, 10, 54);
        WriteUInt32(bytes, 14, 40);
        WriteUInt32(bytes, 18, (uint)width);
        WriteUInt32(bytes, 22, (uint)height);
        WriteUInt16(bytes, 26, 1);
        WriteUInt16(bytes, 28, 24);
        WriteUInt32(bytes, 34, (uint)pixelLength);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                RgbColor pixel = pixels[(row * width) + column];
                int offset = 54 + (row * rowLength) + (column * 3);
                bytes[offset] = pixel.Blue;
                bytes[offset + 1] = pixel.Green;
                bytes[offset + 2] = pixel.Red;
            }
        }

        return bytes;
    }

    private static string NewTemporaryPath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bmp");
    }

    private static RgbColor Rgb(byte red, byte green, byte blue)
    {
        return new RgbColor(red, green, blue);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private readonly struct RgbColor
    {
        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }

        public RgbColor(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }
    }
}
