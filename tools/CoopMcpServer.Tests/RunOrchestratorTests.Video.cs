using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    private static readonly CaptureRegion ClientWindow = new(GameWindowLocator.WindowRegion, null, 0x1234, "Coop Client 1", 100, 50, 1280, 720);

    private VideoCapture Video(FakeFfmpeg ffmpeg, FakeWindowLocator windows = null, string ffmpegPath = null)
    {
        if (ffmpegPath == null)
        {
            ffmpegPath = Path.Combine(directory, "ffmpeg.exe");
            File.WriteAllText(ffmpegPath, "fake ffmpeg; the runner is faked");
        }
        return new VideoCapture(runs, new CoopMcpServerSettings { FfmpegPath = ffmpegPath }, windows ?? new FakeWindowLocator(ClientWindow), ffmpeg);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public async Task RecordVideoRejectsSecondsOutsideOneToOneHundredTwenty(int seconds)
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Video(ffmpeg).RecordAsync(run.RunId, "client1", seconds, default));
        Assert.Empty(ffmpeg.Calls);
    }

    [Fact]
    public async Task RecordVideoRejectsUnownedRunAndInstance()
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg();
        await Assert.ThrowsAsync<ArgumentException>(() => Video(ffmpeg).RecordAsync("missing", "client1", 5, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Video(ffmpeg).RecordAsync(run.RunId, "client2", 5, default));
        Assert.Empty(ffmpeg.Calls);
    }

    [Fact]
    public async Task RecordVideoReportsMissingConfiguredFfmpegWithoutRecording()
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg();
        string missing = Path.Combine(directory, "missing", "ffmpeg.exe");
        var result = await Video(ffmpeg, ffmpegPath: missing).RecordAsync(run.RunId, "client1", 5, default);
        Assert.True(result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.False(metadata.GetProperty("complete").GetBoolean());
        Assert.Equal("ffmpeg_unavailable", metadata.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains(missing, metadata.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(ffmpeg.Calls);
    }

    [Fact]
    public async Task RecordVideoWritesNamedMp4AndContactSheetIntoRunDirectory()
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg();
        var result = await Video(ffmpeg).RecordAsync(run.RunId, "client1", 5, default);
        Assert.NotEqual(true, result.IsError);
        var metadata = result.StructuredContent.Value;
        string path = metadata.GetProperty("path").GetString(), sheet = metadata.GetProperty("contactSheetPath").GetString();
        Assert.Equal(run.ArtifactDirectory, Path.GetDirectoryName(path));
        Assert.Matches(new Regex("^client1-video-[0-9a-f]{32}\\.mp4$"), Path.GetFileName(path));
        Assert.Equal(Path.ChangeExtension(path, null) + "-sheet.png", sheet);
        Assert.Equal(new FileInfo(path).Length, metadata.GetProperty("bytes").GetInt64());
        Assert.Equal(VideoCapture.Fps, metadata.GetProperty("fps").GetInt32());
        Assert.Equal(100, metadata.GetProperty("frames").GetInt32());
        Assert.Equal(5.0, metadata.GetProperty("durationSeconds").GetDouble());
        Assert.Equal("window_region", metadata.GetProperty("targetingMode").GetString());
        Assert.Equal("Coop Client 1", metadata.GetProperty("targeting").GetProperty("windowTitle").GetString());
        Assert.Equal(new[] { 0, 0.625, 1.25, 1.875, 2.5, 3.125, 3.75, 4.375 },
            metadata.GetProperty("contactSheetTimestampsSeconds").EnumerateArray().Select(t => t.GetDouble()));
        Assert.Equal("image/png", Assert.IsType<ImageContentBlock>(result.Content[1]).MimeType);
        var record = ffmpeg.Calls[0];
        Assert.Equal(new[] { "-offset_x", "100", "-offset_y", "50", "-video_size", "1280x720", "-i", "desktop", "-t", "5" },
            record.SkipWhile(a => a != "-offset_x").Take(10));
        Assert.Equal(path, record[^1]);
        Assert.Equal(sheet, ffmpeg.Calls[1][^1]);
        Assert.Contains("fps=1.6,scale=480:-2,tile=4x2", ffmpeg.Calls[1]);
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task RecordVideoReportsPrimaryDisplayFallbackReason()
    {
        var run = await runs.StartAsync("test", 1, default);
        var fallback = new CaptureRegion(GameWindowLocator.PrimaryDisplay, "The instance window is minimized.", 0, null, 0, 0, 1920, 1080);
        var result = await Video(new FakeFfmpeg(), new FakeWindowLocator(fallback)).RecordAsync(run.RunId, "server", 2, default);
        Assert.NotEqual(true, result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("primary_display", metadata.GetProperty("targetingMode").GetString());
        Assert.Equal("The instance window is minimized.", metadata.GetProperty("targeting").GetProperty("fallbackReason").GetString());
        Assert.StartsWith("server-video-", Path.GetFileName(metadata.GetProperty("path").GetString()));
    }

    [Fact]
    public async Task RecordVideoFailureReturnsFfmpegDiagnosticsWithoutContactSheet()
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg { RecordExitCode = 1 };
        var result = await Video(ffmpeg).RecordAsync(run.RunId, "client1", 3, default);
        Assert.True(result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("recording_failed", metadata.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(1, metadata.GetProperty("ffmpeg").GetProperty("exitCode").GetInt32());
        Assert.Equal("fake gdigrab failure", metadata.GetProperty("ffmpeg").GetProperty("errorTail").GetString());
        Assert.Single(ffmpeg.Calls);
    }

    [Fact]
    public async Task RecordVideoRejectsExitedInstanceWithoutRecording()
    {
        var run = await runs.StartAsync("test", 1, default);
        await launcher.Processes[1].StopAsync(TimeSpan.Zero);
        var ffmpeg = new FakeFfmpeg();
        var result = await Video(ffmpeg).RecordAsync(run.RunId, "client1", 3, default);
        Assert.True(result.IsError);
        Assert.Equal("process_exited", result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(ffmpeg.Calls);
    }

    private sealed class FakeWindowLocator(CaptureRegion region) : IGameWindowLocator
    {
        public CaptureRegion Resolve(int pid) => region;
    }

    // Writes the requested output file in place of ffmpeg, so artifact naming and parsing run without a capture device.
    private sealed class FakeFfmpeg : IFfmpegRunner
    {
        public List<string[]> Calls = new();
        public int RecordExitCode;
        public Task<FfmpegResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls.Add(arguments.ToArray());
            bool record = arguments.Contains("gdigrab");
            if (record && RecordExitCode != 0) return Task.FromResult(new FfmpegResult(RecordExitCode, false, "progress=end\n", "fake gdigrab failure"));
            File.WriteAllBytes(arguments[^1], record ? new byte[1234] : new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            string seconds = record ? arguments[arguments.ToList().IndexOf("-t") + 1] : "0";
            return Task.FromResult(new FfmpegResult(0, false, record ? "frame=10\nprogress=continue\nframe=" + (int.Parse(seconds) * VideoCapture.Fps) + "\nprogress=end\n" : "", ""));
        }
    }
}
