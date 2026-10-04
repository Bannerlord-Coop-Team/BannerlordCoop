using ModelContextProtocol.Protocol;
using System.Text.RegularExpressions;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    private static readonly CaptureRegion ClientWindow = new(GameWindowLocator.WindowRegion, null, 0x1234, "Coop Client 1", 100, 50, 1280, 720,
        true, new[] { new OccludingWindow("Coop Server", 4242) });

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
    public async Task RecordVideoCapturesWindowSurfaceByHandleIntoNamedRunArtifacts()
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
        Assert.Equal("window_capture", metadata.GetProperty("targetingMode").GetString());
        var targeting = metadata.GetProperty("targeting");
        Assert.Equal("Coop Client 1", targeting.GetProperty("windowTitle").GetString());
        // The covered window is still recorded from its own surface; occlusion is reported for the agent.
        Assert.True(targeting.GetProperty("occluded").GetBoolean());
        Assert.Equal("Coop Server", targeting.GetProperty("occludingWindows")[0].GetProperty("title").GetString());
        Assert.Equal(4242, targeting.GetProperty("occludingWindows")[0].GetProperty("pid").GetInt32());
        Assert.Equal(new[] { 0, 0.625, 1.25, 1.875, 2.5, 3.125, 3.75, 4.375 },
            metadata.GetProperty("contactSheetTimestampsSeconds").EnumerateArray().Select(t => t.GetDouble()));
        Assert.Equal("image/png", Assert.IsType<ImageContentBlock>(result.Content[1]).MimeType);
        Assert.Equal(new[] { "-hide_banner", "-h", "filter=gfxcapture" }, ffmpeg.Calls[0]);
        var record = ffmpeg.Calls[1];
        Assert.StartsWith("gfxcapture=hwnd=4660:max_framerate=20:capture_cursor=0,hwdownload,format=bgra,fps=20,", record[Array.IndexOf(record, "-filter_complex") + 1]);
        Assert.Equal("5", record[Array.IndexOf(record, "-t") + 1]);
        Assert.Equal(VideoCapture.StallTimeout, ffmpeg.StallTimeouts[1]);
        Assert.Equal(path, record[^1]);
        Assert.Equal(sheet, ffmpeg.Calls[2][^1]);
        Assert.Contains("fps=1.6,scale=480:-2,tile=4x2", ffmpeg.Calls[2]);
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task RecordVideoFallsBackToScreenRegionWhenWindowCaptureDeliversNoFrames()
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg { StallWindowCapture = true };
        var result = await Video(ffmpeg).RecordAsync(run.RunId, "client1", 3, default);
        Assert.NotEqual(true, result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("window_region", metadata.GetProperty("targetingMode").GetString());
        Assert.StartsWith("Window capture produced no frames", metadata.GetProperty("targeting").GetProperty("fallbackReason").GetString());
        Assert.True(metadata.GetProperty("targeting").GetProperty("occluded").GetBoolean());
        Assert.Equal(new[] { "-offset_x", "100", "-offset_y", "50", "-video_size", "1280x720", "-i", "desktop", "-t", "3" },
            ffmpeg.Calls[2].SkipWhile(a => a != "-offset_x").Take(10));
    }

    [Fact]
    public async Task RecordVideoUsesScreenRegionWhenFfmpegLacksWindowCapture()
    {
        var run = await runs.StartAsync("test", 1, default);
        var ffmpeg = new FakeFfmpeg { SupportsWindowCapture = false };
        var result = await Video(ffmpeg).RecordAsync(run.RunId, "client1", 2, default);
        Assert.NotEqual(true, result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("window_region", metadata.GetProperty("targetingMode").GetString());
        Assert.Contains("no gfxcapture hwnd option", metadata.GetProperty("targeting").GetProperty("fallbackReason").GetString());
        Assert.Contains("gdigrab", ffmpeg.Calls[1]);
    }

    [Fact]
    public async Task RecordVideoReportsPrimaryDisplayFallbackReason()
    {
        var run = await runs.StartAsync("test", 1, default);
        var fallback = new CaptureRegion(GameWindowLocator.PrimaryDisplay, "The instance window is minimized.", 0, null, 0, 0, 1920, 1080, false, Array.Empty<OccludingWindow>());
        var ffmpeg = new FakeFfmpeg();
        var result = await Video(ffmpeg, new FakeWindowLocator(fallback)).RecordAsync(run.RunId, "server", 2, default);
        Assert.NotEqual(true, result.IsError);
        var metadata = result.StructuredContent.Value;
        Assert.Equal("primary_display", metadata.GetProperty("targetingMode").GetString());
        Assert.Equal("The instance window is minimized.", metadata.GetProperty("targeting").GetProperty("fallbackReason").GetString());
        Assert.StartsWith("server-video-", Path.GetFileName(metadata.GetProperty("path").GetString()));
        Assert.Contains("gdigrab", ffmpeg.Calls[0]);
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
        Assert.Equal("fake capture failure", metadata.GetProperty("ffmpeg").GetProperty("errorTail").GetString());
        Assert.Equal(2, ffmpeg.Calls.Count);
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

    [Fact]
    public void FindOccludersReportsOnlyDrawnWindowsIntersectingTheTarget()
    {
        var target = new ScreenRect(568, 211, 2008, 1291);
        WindowSnapshot Window(string title, ScreenRect bounds, bool visible = true, bool minimized = false, bool cloaked = false, bool clickThrough = false) =>
            new(title, title.Length, bounds, visible, minimized, cloaked, clickThrough);
        var above = new[]
        {
            Window("Coop Server", new ScreenRect(568, 211, 2008, 1291)),
            Window("hidden", target, visible: false),
            Window("minimized", target, minimized: true),
            Window("cloaked store app", target, cloaked: true),
            Window("overlay", target, clickThrough: true),
            Window("left of target", new ScreenRect(0, 211, 568, 1291)),
            Window("empty", new ScreenRect(700, 300, 700, 300)),
            Window("corner", new ScreenRect(2000, 1280, 2100, 1400)),
        };
        var occluders = GameWindowLocator.FindOccluders(target, above);
        Assert.Equal(new[] { "Coop Server", "corner" }, occluders.Select(o => o.Title));
        Assert.Equal(11, occluders[0].Pid);
        Assert.Equal(GameWindowLocator.MaxOccluders, GameWindowLocator.FindOccluders(target, Enumerable.Repeat(above[0], 40)).Length);
    }

    private sealed class FakeWindowLocator(CaptureRegion region) : IGameWindowLocator
    {
        public CaptureRegion Resolve(int pid) => region;
    }

    // Writes the requested output file in place of ffmpeg, so artifact naming, mode selection and parsing run without a capture device.
    private sealed class FakeFfmpeg : IFfmpegRunner
    {
        public List<string[]> Calls = new();
        public List<TimeSpan?> StallTimeouts = new();
        public int RecordExitCode;
        public bool SupportsWindowCapture = true;
        public bool StallWindowCapture;
        public Task<FfmpegResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, TimeSpan? stallTimeout, CancellationToken cancellationToken)
        {
            Calls.Add(arguments.ToArray());
            StallTimeouts.Add(stallTimeout);
            if (arguments.Contains("filter=gfxcapture"))
                return Task.FromResult(new FfmpegResult(0, false, false, SupportsWindowCapture ? "Filter gfxcapture\n   hwnd <uint64>\n" : "Unknown filter 'gfxcapture'.\n", ""));
            bool windowCapture = arguments.Contains("-filter_complex");
            if (windowCapture && StallWindowCapture) return Task.FromResult(new FfmpegResult(null, false, true, "frame=0\nprogress=continue\n", "no frames"));
            bool record = windowCapture || arguments.Contains("gdigrab");
            if (record && RecordExitCode != 0) return Task.FromResult(new FfmpegResult(RecordExitCode, false, false, "progress=end\n", "fake capture failure"));
            File.WriteAllBytes(arguments[^1], record ? new byte[1234] : new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            string seconds = record ? arguments[arguments.ToList().IndexOf("-t") + 1] : "0";
            return Task.FromResult(new FfmpegResult(0, false, false, record ? "frame=10\nprogress=continue\nframe=" + (int.Parse(seconds) * VideoCapture.Fps) + "\nprogress=end\n" : "", ""));
        }
    }
}
