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

    [McpServerTool(Name = "record_video", ReadOnly = true), Description("Record 1..120 seconds of one owned instance's game window with local ffmpeg (ffmpegPath in profiles.json, else PATH), blocking until done. Captures the window's on-screen client area (window_region); overlapping windows appear in it. Falls back to the primary display (primary_display, with fallbackReason) when the window is missing, minimized or off-screen. Writes an H.264 mp4 (20 fps, max 1920 wide, 4 Mb/s) and a 4x2 contact-sheet PNG of 8 evenly spaced frames into the run artifact directory, returning path, durationSeconds, fps, frames, bytes, targetingMode, contactSheetPath, contactSheetTimestampsSeconds and the sheet image. No game command is sent. No retries; failures return ffmpeg diagnostics.")]
    public Task<CallToolResult> RecordVideo(string run_id, string instance, int seconds, CancellationToken cancellationToken) =>
        video.RecordAsync(run_id, instance, seconds, cancellationToken);
}
