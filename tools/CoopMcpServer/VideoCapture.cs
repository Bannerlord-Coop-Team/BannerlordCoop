using Common.LiveTesting;
using ModelContextProtocol.Protocol;
using System.Globalization;
using System.Text.Json;

namespace CoopMcpServer;

public interface IVideoCapture
{
    Task<CallToolResult> RecordAsync(string runId, string instance, int seconds, CancellationToken cancellationToken);
}

public sealed class VideoCapture : IVideoCapture
{
    public const int Fps = 20;
    public const int MaxOutputWidth = 1920;
    public const string Bitrate = "4M";
    public const int SheetFrames = 8;
    public const int SheetColumns = 4;
    public const int SheetTileWidth = 480;
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(10);
    private const int MaxEmbeddedSheetBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan FinalizeGrace = TimeSpan.FromSeconds(30);
    private static readonly string ScaleFilter = "scale='trunc(min(" + MaxOutputWidth + ",iw)/2)*2':-2";
    private readonly IRunOrchestrator runs;
    private readonly CoopMcpServerSettings settings;
    private readonly IGameWindowLocator windows;
    private readonly IFfmpegRunner ffmpeg;

    public VideoCapture(IRunOrchestrator runs, CoopMcpServerSettings settings, IGameWindowLocator windows, IFfmpegRunner ffmpeg)
    {
        this.runs = runs;
        this.settings = settings;
        this.windows = windows;
        this.ffmpeg = ffmpeg;
    }

    public async Task<CallToolResult> RecordAsync(string runId, string instance, int seconds, CancellationToken cancellationToken)
    {
        if (seconds < 1 || seconds > 120) throw new ArgumentOutOfRangeException(nameof(seconds), "seconds must be 1..120.");
        var target = runs.ResolveVideoTarget(runId, instance);
        string executable = LocateFfmpeg(out string ffmpegProblem);
        if (executable == null) return Failure(target, null, null, null, "ffmpeg_unavailable", ffmpegProblem);
        if (!target.ProcessAlive) return Failure(target, executable, null, null, "process_exited", "The owned instance process has exited; nothing was recorded.");
        CaptureRegion region = windows.Resolve(target.Pid);
        FfmpegResult recording = null;
        try
        {
            TimeSpan budget = TimeSpan.FromSeconds(seconds) + FinalizeGrace;
            if (region.Mode == GameWindowLocator.WindowRegion)
            {
                if (!await SupportsWindowCaptureAsync(executable, cancellationToken))
                    region = region with { FallbackReason = "This ffmpeg has no gfxcapture hwnd option; recorded the on-screen window region, which includes windows drawn over it." };
                else
                {
                    recording = await ffmpeg.RunAsync(executable, WindowCaptureArguments(region.WindowHandle, seconds, target.VideoPath), budget, StallTimeout, cancellationToken);
                    if (recording.Stalled && LastProgressValue(recording.Progress, "frame") == 0)
                    {
                        region = region with { FallbackReason = "Window capture produced no frames within " + StallTimeout.TotalSeconds + " s; recorded the on-screen window region instead. gfxcapture stderr: " + recording.ErrorTail };
                        recording = null;
                    }
                    else region = region with { Mode = GameWindowLocator.WindowCapture };
                }
            }
            recording ??= await ffmpeg.RunAsync(executable, RecordArguments(region, seconds, target.VideoPath), budget, StallTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(target, executable, region, null, "operation_cancelled", "Recording was cancelled and ffmpeg was stopped; the mp4 is incomplete.");
        }
        catch (Exception e) when (e is IOException || e is System.ComponentModel.Win32Exception || e is InvalidOperationException)
        {
            return Failure(target, executable, region, null, "ffmpeg_start_failed", e.Message);
        }
        int frames = LastProgressValue(recording.Progress, "frame");
        if (recording.TimedOut || recording.Stalled || recording.ExitCode != 0 || frames <= 0 || !File.Exists(target.VideoPath))
            return Failure(target, executable, region, recording, "recording_failed",
                recording.TimedOut ? "ffmpeg did not finish within the recording budget and was stopped."
                : recording.Stalled ? "Capture stopped delivering frames for " + StallTimeout.TotalSeconds + " s and ffmpeg was stopped; the mp4 is incomplete."
                : "ffmpeg did not produce a complete recording.");
        double duration = Math.Round((double)frames / Fps, 3);
        double[] timestamps = Enumerable.Range(0, SheetFrames).Select(i => Math.Round(i * duration / SheetFrames, 3)).ToArray();
        FfmpegResult sheet;
        try
        {
            sheet = await ffmpeg.RunAsync(executable, SheetArguments(target.VideoPath, duration, target.ContactSheetPath), FinalizeGrace, null, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            sheet = new FfmpegResult(null, false, false, "", "Contact sheet generation was cancelled.");
        }
        var metadata = new Dictionary<string, object>
        {
            ["complete"] = true, ["path"] = target.VideoPath, ["durationSeconds"] = duration, ["requestedSeconds"] = seconds, ["fps"] = Fps,
            ["frames"] = frames, ["bytes"] = new FileInfo(target.VideoPath).Length, ["targetingMode"] = region.Mode, ["targeting"] = region,
            ["contactSheetTimestampsSeconds"] = timestamps, ["ffmpegPath"] = executable,
        };
        if (sheet.ExitCode != 0 || !File.Exists(target.ContactSheetPath))
        {
            metadata["contactSheetPath"] = null;
            metadata["error"] = new LiveTestError("contact_sheet_failed", "The mp4 is complete but the contact sheet was not produced.", false);
            metadata["ffmpeg"] = Diagnostics(sheet);
            return Result(metadata, true, null);
        }
        metadata["contactSheetPath"] = target.ContactSheetPath;
        byte[] png = File.ReadAllBytes(target.ContactSheetPath);
        return Result(metadata, false, png.Length <= MaxEmbeddedSheetBytes ? png : null);
    }

    private string LocateFfmpeg(out string problem)
    {
        problem = null;
        string configured = settings.FfmpegPath;
        if (!string.IsNullOrEmpty(configured))
        {
            if (Path.IsPathFullyQualified(configured) && File.Exists(configured)) return configured;
            problem = "Configured ffmpegPath does not name an existing absolute file: " + configured;
            return null;
        }
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string trimmed = directory.Trim().Trim('"');
            if (trimmed.Length == 0 || !Path.IsPathFullyQualified(trimmed)) continue;
            string candidate = Path.Combine(trimmed, "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        problem = "ffmpeg.exe was not found on PATH. Install ffmpeg or set top-level ffmpegPath in profiles.json, then restart the MCP server.";
        return null;
    }

    private async Task<bool> SupportsWindowCaptureAsync(string executable, CancellationToken cancellationToken)
    {
        var help = await ffmpeg.RunAsync(executable, ["-hide_banner", "-h", "filter=gfxcapture"], TimeSpan.FromSeconds(10), null, cancellationToken);
        return help.ExitCode == 0 && help.Progress.Contains("hwnd", StringComparison.Ordinal);
    }

    // Windows.Graphics.Capture of the window's own surface, independent of z-order; fps fills gaps when the window presents slower.
    public static string[] WindowCaptureArguments(long windowHandle, int seconds, string output) =>
    [
        "-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-progress", "pipe:1", "-nostats",
        "-filter_complex", "gfxcapture=hwnd=" + windowHandle.ToString(CultureInfo.InvariantCulture) + ":max_framerate=" + Fps +
            ":capture_cursor=0,hwdownload,format=bgra,fps=" + Fps + "," + ScaleFilter + "[v]",
        "-map", "[v]", "-t", seconds.ToString(CultureInfo.InvariantCulture), .. EncoderArguments(output),
    ];

    public static string[] RecordArguments(CaptureRegion region, int seconds, string output) =>
    [
        "-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-progress", "pipe:1", "-nostats",
        "-f", "gdigrab", "-framerate", Fps.ToString(CultureInfo.InvariantCulture),
        "-offset_x", region.X.ToString(CultureInfo.InvariantCulture), "-offset_y", region.Y.ToString(CultureInfo.InvariantCulture),
        "-video_size", region.Width.ToString(CultureInfo.InvariantCulture) + "x" + region.Height.ToString(CultureInfo.InvariantCulture),
        "-i", "desktop", "-t", seconds.ToString(CultureInfo.InvariantCulture), "-vf", ScaleFilter, .. EncoderArguments(output),
    ];

    private static string[] EncoderArguments(string output) =>
    [
        "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-b:v", Bitrate, "-maxrate", Bitrate, "-bufsize", "8M",
        "-movflags", "+faststart", output,
    ];

    public static string[] SheetArguments(string video, double durationSeconds, string output) =>
    [
        "-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-i", video,
        "-vf", "fps=" + (SheetFrames / durationSeconds).ToString("0.######", CultureInfo.InvariantCulture) + ",scale=" + SheetTileWidth + ":-2,tile=" +
            SheetColumns + "x" + (SheetFrames / SheetColumns),
        "-frames:v", "1", "-update", "1", output,
    ];

    private static int LastProgressValue(string progress, string key)
    {
        int value = 0;
        foreach (string line in (progress ?? "").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(key + "=", StringComparison.Ordinal) && int.TryParse(trimmed.AsSpan(key.Length + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                value = parsed;
        }
        return value;
    }

    private static object Diagnostics(FfmpegResult result) => result == null ? null : new { result.ExitCode, result.TimedOut, result.Stalled, result.ErrorTail };

    private static CallToolResult Failure(VideoTarget target, string executable, CaptureRegion region, FfmpegResult result, string code, string message) =>
        Result(new Dictionary<string, object>
        {
            ["complete"] = false, ["path"] = target.VideoPath, ["targetingMode"] = region?.Mode, ["targeting"] = region, ["ffmpegPath"] = executable,
            ["error"] = new LiveTestError(code, message, false), ["ffmpeg"] = Diagnostics(result),
        }, true, null);

    private static CallToolResult Result(Dictionary<string, object> metadata, bool error, byte[] png)
    {
        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = new CallToolResult { IsError = error, StructuredContent = JsonSerializer.Deserialize<JsonElement>(json),
            Content = new List<ContentBlock> { new TextContentBlock { Text = json } } };
        if (png != null) result.Content.Add(ImageContentBlock.FromBytes(png, "image/png"));
        return result;
    }
}
