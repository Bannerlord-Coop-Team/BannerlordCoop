using Common.Logging;
using Serilog;
using System;

namespace Missions.Agents.Handlers;

public interface IMovementTrafficBudget
{
    int AvailableBytes { get; }
    double BytesPerSecond { get; }
    int BurstBytes { get; }
    void Configure(double bytesPerSecond, int burstBytes);
    void Advance(float elapsedSeconds);
    bool TrySpend(int bytes);
    MovementTrafficFrame ReportFrame(int deferredSnapshots, float maximumDeferredAgeSeconds);
    void Clear();
}

public readonly struct MovementTrafficFrame
{
    public long SentBytes { get; }
    public int DeferredSnapshots { get; }
    public float MaximumDeferredAgeSeconds { get; }

    public MovementTrafficFrame(
        long sentBytes,
        int deferredSnapshots,
        float maximumDeferredAgeSeconds)
    {
        SentBytes = sentBytes;
        DeferredSnapshots = deferredSnapshots;
        MaximumDeferredAgeSeconds = maximumDeferredAgeSeconds;
    }

    public MovementTrafficFrame Add(MovementTrafficFrame other) =>
        new MovementTrafficFrame(
            SentBytes + other.SentBytes,
            DeferredSnapshots + other.DeferredSnapshots,
            Math.Max(MaximumDeferredAgeSeconds, other.MaximumDeferredAgeSeconds));
}

/// <summary>Per-route token bucket that reserves most provider bandwidth for reliable traffic.</summary>
public sealed class MovementTrafficBudget : IMovementTrafficBudget
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementTrafficBudget>();

    // Each recipient owns one budget, so reported bytes already represent its actual route traffic.
    internal const int DefaultBytesPerSecond = 1024 * 1024;
    internal const int DefaultBurstBytes = 128 * 1024;
    private const float ReportIntervalSeconds = 1f;

    private double bytesPerSecond;
    private int burstBytes;
    private double availableBytes;
    private float reportElapsed;
    private long sentBytes;
    private long frameSentBytes;
    private int maximumDeferredSnapshots;
    private float maximumDeferredAgeSeconds;

    public MovementTrafficBudget(
        double bytesPerSecond = DefaultBytesPerSecond,
        int burstBytes = DefaultBurstBytes)
    {
        if (double.IsNaN(bytesPerSecond) ||
            double.IsInfinity(bytesPerSecond) ||
            bytesPerSecond <= 0d)
            throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
        if (burstBytes <= 0) throw new ArgumentOutOfRangeException(nameof(burstBytes));

        this.bytesPerSecond = bytesPerSecond;
        this.burstBytes = burstBytes;
        availableBytes = burstBytes;
    }

    public int AvailableBytes => (int)Math.Floor(availableBytes);
    public double BytesPerSecond => bytesPerSecond;
    public int BurstBytes => burstBytes;

    public void Configure(double bytesPerSecond, int burstBytes)
    {
        if (double.IsNaN(bytesPerSecond) ||
            double.IsInfinity(bytesPerSecond) ||
            bytesPerSecond <= 0d)
            throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
        if (burstBytes <= 0) throw new ArgumentOutOfRangeException(nameof(burstBytes));
        if (this.bytesPerSecond == bytesPerSecond && this.burstBytes == burstBytes) return;

        this.bytesPerSecond = bytesPerSecond;
        this.burstBytes = burstBytes;
        availableBytes = Math.Min(availableBytes, burstBytes);
    }

    public void Advance(float elapsedSeconds)
    {
        frameSentBytes = 0;
        if (elapsedSeconds <= 0f) return;

        availableBytes = Math.Min(
            burstBytes,
            availableBytes + ((double)bytesPerSecond * elapsedSeconds));
        reportElapsed += elapsedSeconds;
    }

    public bool TrySpend(int bytes)
    {
        if (bytes <= 0 || bytes > AvailableBytes) return false;

        availableBytes -= bytes;
        sentBytes += bytes;
        frameSentBytes += bytes;
        return true;
    }

    public MovementTrafficFrame ReportFrame(int deferredSnapshots, float maximumDeferredAgeSeconds)
    {
        var frame = new MovementTrafficFrame(
            frameSentBytes,
            deferredSnapshots,
            maximumDeferredAgeSeconds);
        maximumDeferredSnapshots = Math.Max(maximumDeferredSnapshots, deferredSnapshots);
        this.maximumDeferredAgeSeconds = Math.Max(
            this.maximumDeferredAgeSeconds,
            maximumDeferredAgeSeconds);
        if (reportElapsed < ReportIntervalSeconds) return frame;

        Logger.Information(
            "[BattleTraffic] Movement {BytesPerSecond:0} payload bytes/s per route, {Deferred} deferred snapshot(s), " +
            "maximum deferred age {MaximumAge:0.000}s, {Available} budget bytes available",
            sentBytes / Math.Max(reportElapsed, 0.001f),
            maximumDeferredSnapshots,
            this.maximumDeferredAgeSeconds,
            AvailableBytes);

        reportElapsed = 0f;
        sentBytes = 0;
        maximumDeferredSnapshots = 0;
        this.maximumDeferredAgeSeconds = 0f;
        return frame;
    }

    public void Clear()
    {
        availableBytes = burstBytes;
        reportElapsed = 0f;
        sentBytes = 0;
        frameSentBytes = 0;
        maximumDeferredSnapshots = 0;
        maximumDeferredAgeSeconds = 0f;
    }
}
