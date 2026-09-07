using CoopMcpServer.TestHost;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace CoopMcpServer.Tests;

public sealed class ScreenshotCaptureTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CoopCaptureTest-" + Guid.NewGuid().ToString("N"));
    public ScreenshotCaptureTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task OfficialSdkStdioReturnsCompletedPngImageAndArtifactMetadata()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Harmless screenshot protocol fixture",
            Command = Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe"),
            Arguments = new[] { directory }, ShutdownTimeout = TimeSpan.FromSeconds(10),
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        File.WriteAllText(Path.Combine(directory, "Danustica campaign.sav"), "opaque test save");
        var saves = await client.CallToolAsync("list_saves", new Dictionary<string, object> { ["profile"] = "fixture" }, cancellationToken: timeout.Token);
        Assert.NotEqual(true, saves.IsError);
        Assert.Equal("Danustica campaign", saves.StructuredContent.Value.GetProperty("saves")[0].GetProperty("name").GetString());
        var result = await client.CallToolAsync("capture_screenshot", new Dictionary<string, object>
        {
            ["run_id"] = "fixture", ["instance"] = "client1", ["timeout_seconds"] = 5,
        }, cancellationToken: timeout.Token);
        // The SDK omits the optional isError field on successful wire responses.
        Assert.NotEqual(true, result.IsError);
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", image.MimeType);
        byte[] png = image.DecodedData.ToArray();
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        int compressedLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33, 4));
        using var compressed = new MemoryStream(png, 41, compressedLength);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var pixels = new MemoryStream();
        zlib.CopyTo(pixels);
        Assert.Equal(14, pixels.Length);
        Assert.Equal(new byte[] { 0, 255, 0, 0, 0, 0, 255, 0, 0, 0, 255, 0, 255, 0 }, pixels.ToArray());
        var metadata = result.StructuredContent.Value;
        Assert.True(metadata.GetProperty("complete").GetBoolean());
        Assert.False(metadata.GetProperty("semanticVisualCorrectnessEvaluated").GetBoolean());
        Assert.Equal(png, File.ReadAllBytes(metadata.GetProperty("path").GetString()));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(png)), metadata.GetProperty("sha256").GetString());
    }

    [Fact]
    public async Task OfficialSdkStdioPreservesBridgeFailureWithoutImage()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Harmless screenshot failure fixture",
            Command = Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe"),
            Arguments = new[] { directory, "screenshot" }, ShutdownTimeout = TimeSpan.FromSeconds(10),
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        var result = await client.CallToolAsync("capture_screenshot", new Dictionary<string, object>
        {
            ["run_id"] = "fixture", ["instance"] = "client1", ["timeout_seconds"] = 5,
        }, cancellationToken: timeout.Token);
        Assert.True(result.IsError);
        Assert.Empty(result.Content.OfType<ImageContentBlock>());
        var error = result.StructuredContent.Value.GetProperty("response").GetProperty("error");
        Assert.Equal("stale_snapshot", error.GetProperty("code").GetString());
        Assert.Equal("exact bridge diagnostic", error.GetProperty("message").GetString());
        Assert.True(error.GetProperty("outcomeUncertain").GetBoolean());
    }

    [Fact]
    public async Task RequestAckIsNotCompletionAndCaptureIsNeverRepeated()
    {
        var runs = new ScreenshotRunFixture(directory);
        var result = await new ScreenshotCapture(runs, new ScreenshotImageEncoder()).CaptureAsync("run", "client1", 5, default);
        Assert.False(result.IsError);
        Assert.Equal(1, runs.Methods.Count(m => m == "screenshot"));
        Assert.Equal(2, runs.Methods.Count(m => m == "screenshot-status"));
        Assert.Equal(2, runs.Methods.Count(m => m == "render-status"));
    }

    [Theory]
    [InlineData("render-status", "client_only")]
    [InlineData("screenshot", "game_thread_timeout")]
    [InlineData("screenshot-status", "screenshot_quality_rejected")]
    public async Task BridgeFailuresRemainExactAndDoNotProduceImages(string method, string code)
    {
        var runs = new ScreenshotRunFixture(directory) { FailureMethod = method, FailureCode = code };
        var result = await new ScreenshotCapture(runs, new ScreenshotImageEncoder()).CaptureAsync("run", "client1", 3, default);
        Assert.True(result.IsError);
        Assert.Empty(result.Content.OfType<ImageContentBlock>());
        var error = result.StructuredContent.Value.GetProperty("response").GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal("exact bridge diagnostic", error.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeadlineDistinguishesNoRenderFromPendingKnownCapture(bool frozen)
    {
        var runs = new ScreenshotRunFixture(directory) { FrozenRender = frozen, PendingCapture = !frozen };
        var result = await new ScreenshotCapture(runs, new ScreenshotImageEncoder()).CaptureAsync("run", "client1", 1, default);
        Assert.True(result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("capture_deadline_expired", metadata.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(!frozen, metadata.GetProperty("error").GetProperty("outcomeUncertain").GetBoolean());
        Assert.Equal(frozen ? 0 : 1, runs.Methods.Count(m => m == "screenshot"));
    }

    [Theory]
    [InlineData("render-status", false)]
    [InlineData("screenshot-status", true)]
    public async Task CaptureDeadlineRetainsTransportCancellationResponse(string method, bool requested)
    {
        var runs = new ScreenshotRunFixture(directory) { CancelResponseMethod = method };
        var result = await new ScreenshotCapture(runs, new ScreenshotImageEncoder()).CaptureAsync("run", "client1", 1, default);
        Assert.True(result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("capture_deadline_expired", metadata.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(requested, metadata.GetProperty("error").GetProperty("outcomeUncertain").GetBoolean());
        Assert.Equal("operation_cancelled", metadata.GetProperty("response").GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(requested ? 1 : 0, runs.Methods.Count(m => m == "screenshot"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedEvidenceOrDifferentPathIsRejected(bool path)
    {
        var runs = new ScreenshotRunFixture(directory) { WrongPath = path, WrongHash = !path };
        var result = await new ScreenshotCapture(runs, new ScreenshotImageEncoder()).CaptureAsync("run", "client1", 3, default);
        Assert.True(result.IsError);
        Assert.Empty(result.Content.OfType<ImageContentBlock>());
    }

    [Fact]
    public void EncoderRejectsUnboundedOrMalformedBmpBeforeAllocatingPixelRows()
    {
        string path = Path.Combine(directory, "bad.bmp");
        byte[] bmp = ScreenshotRunFixture.Bmp();
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18, 4), int.MaxValue);
        File.WriteAllBytes(path, bmp);
        Assert.Throws<IOException>(() => new ScreenshotImageEncoder().Encode(path, Convert.ToHexString(SHA256.HashData(bmp))));
        using (var file = new FileStream(path, FileMode.Create)) file.SetLength(ScreenshotImageEncoder.MaximumBmpBytes + 1L);
        Assert.Throws<IOException>(() => new ScreenshotImageEncoder().Encode(path, "unused"));
    }
    [Fact]
    public async Task CallerCancellationIsNotMisreportedAsDeadlineOrRetried()
    {
        var runs = new ScreenshotRunFixture(directory) { PendingCapture = true };
        using var cancelled = new CancellationTokenSource();
        var capture = new ScreenshotCapture(runs, new ScreenshotImageEncoder()).CaptureAsync("run", "client1", 10, cancelled.Token);
        using var observationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!runs.Methods.Contains("screenshot-status")) await Task.Delay(10, observationTimeout.Token);
        cancelled.Cancel();
        var result = await capture;
        Assert.Equal("operation_cancelled", result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(1, runs.Methods.Count(m => m == "screenshot"));
    }

    public void Dispose() => Directory.Delete(directory, true);
}
