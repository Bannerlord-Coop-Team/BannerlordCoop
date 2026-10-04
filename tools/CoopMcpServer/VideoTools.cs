using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CoopMcpServer;

public interface IVideoTools
{
    Task<CallToolResult> RecordVideo(string run_id, string instance, int seconds, CancellationToken cancellationToken);
}

[McpServerToolType]
public sealed class VideoTools : IVideoTools
{
    private readonly IVideoCapture video;
    public VideoTools(IVideoCapture video) { this.video = video; }

    [McpServerTool(Name = "record_video", ReadOnly = true), Description("Record 1..120 seconds of one owned instance's game window with local ffmpeg (ffmpegPath in profiles.json, else PATH), blocking until done. Captures the owned window's own surface by handle with gfxcapture (window_capture), even when other windows cover it. If that is unavailable or yields no frames within 10 s it records the on-screen client area (window_region, fallbackReason); a missing, minimized or off-screen window records the primary display (primary_display). targeting.occluded/occludingWindows list windows drawn over the target. Writes an H.264 mp4 (20 fps, max 1920 wide, 4 Mb/s) and a 4x2 contact-sheet PNG of 8 evenly spaced frames into the run artifact directory, returning path, durationSeconds, fps, frames, bytes, targetingMode, contactSheetPath, contactSheetTimestampsSeconds and the sheet image. No game command is sent. No retries; failures return ffmpeg diagnostics.")]
    public Task<CallToolResult> RecordVideo(string run_id, string instance, int seconds, CancellationToken cancellationToken) =>
        video.RecordAsync(run_id, instance, seconds, cancellationToken);
}
