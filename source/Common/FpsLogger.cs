using Common.Logging;
using Serilog;
using System;

namespace Common;

public class FpsLogger : IUpdateable
{
    private static readonly ILogger Logger = LogManager.GetLogger<FpsLogger>();

    private static readonly TimeSpan DefaultReportInterval = TimeSpan.FromSeconds(30);

    private readonly TimeSpan reportInterval;
    private readonly Action<FpsWindowStats> reportSink;
    private double windowSeconds;
    private int windowFrames;
    private double minimumFps = double.MaxValue;
    private double maximumFps = double.MinValue;

    public FpsLogger() : this(DefaultReportInterval, LogReport)
    {
    }

    public FpsLogger(TimeSpan reportInterval, Action<FpsWindowStats> reportSink)
    {
        if (reportSink == null) throw new ArgumentNullException(nameof(reportSink));

        this.reportInterval = reportInterval > TimeSpan.Zero ? reportInterval : DefaultReportInterval;
        this.reportSink = reportSink;
    }

    public int Priority { get; } = UpdatePriority.MainLoop.Fps;

    public void Update(TimeSpan frameTime)
    {
        if (frameTime <= TimeSpan.Zero) return;

        windowFrames++;
        windowSeconds += frameTime.TotalSeconds;

        double fps = 1d / frameTime.TotalSeconds;
        if (fps < minimumFps) minimumFps = fps;
        if (fps > maximumFps) maximumFps = fps;

        if (windowSeconds >= reportInterval.TotalSeconds)
        {
            reportSink(new FpsWindowStats(
                windowFrames,
                windowSeconds,
                windowFrames / windowSeconds,
                minimumFps,
                maximumFps));
            ResetWindow();
        }
    }

    private void ResetWindow()
    {
        windowFrames = 0;
        windowSeconds = 0d;
        minimumFps = double.MaxValue;
        maximumFps = double.MinValue;
    }

    private static void LogReport(FpsWindowStats stats)
    {
        Logger.Information(
            "[Fps] frames={Frames} seconds={Seconds:0.00} avg={Average:0.0} min={Minimum:0.0} max={Maximum:0.0}",
            stats.Frames,
            stats.Seconds,
            stats.AverageFps,
            stats.MinimumFps,
            stats.MaximumFps);
    }
}

public struct FpsWindowStats
{
    public int Frames { get; }
    public double Seconds { get; }
    public double AverageFps { get; }
    public double MinimumFps { get; }
    public double MaximumFps { get; }

    public FpsWindowStats(int frames, double seconds, double averageFps, double minimumFps, double maximumFps)
    {
        Frames = frames;
        Seconds = seconds;
        AverageFps = averageFps;
        MinimumFps = minimumFps;
        MaximumFps = maximumFps;
    }
}
