using Common.Logging;
using Serilog;
using System;

namespace Missions.Agents.Handlers;

public interface IMovementTrafficBudget
{
    int AvailableBytes { get; }
    void Advance(float elapsedSeconds);
    bool TrySpend(int bytes);
    void ReportFrame(int deferredSnapshots, float maximumDeferredAgeSeconds);
    void Clear();
}

/// <summary>Per-route token bucket that reserves most provider bandwidth for reliable traffic.</summary>
public sealed class MovementTrafficBudget : IMovementTrafficBudget
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementTrafficBudget>();

    // SendAll is charged once, so these defaults assume two-player battles; actual egress scales with peers.
    internal const int DefaultBytesPerSecond = 1024 * 1024;
    internal const int DefaultBurstBytes = 128 * 1024;
    private const float ReportIntervalSeconds = 1f;

    private readonly int bytesPerSecond;
    private readonly int burstBytes;
    private double availableBytes;
    private float reportElapsed;
    private long sentBytes;
    private int maximumDeferredSnapshots;
    private float maximumDeferredAgeSeconds;

    public MovementTrafficBudget(
        int bytesPerSecond = DefaultBytesPerSecond,
        int burstBytes = DefaultBurstBytes)
    {
        if (bytesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
        if (burstBytes <= 0) throw new ArgumentOutOfRangeException(nameof(burstBytes));

        this.bytesPerSecond = bytesPerSecond;
        this.burstBytes = burstBytes;
        availableBytes = burstBytes;
    }

    public int AvailableBytes => (int)Math.Floor(availableBytes);

    public void Advance(float elapsedSeconds)
    {
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
        return true;
    }

    public void ReportFrame(int deferredSnapshots, float maximumDeferredAgeSeconds)
    {
        maximumDeferredSnapshots = Math.Max(maximumDeferredSnapshots, deferredSnapshots);
        this.maximumDeferredAgeSeconds = Math.Max(
            this.maximumDeferredAgeSeconds,
            maximumDeferredAgeSeconds);
        if (reportElapsed < ReportIntervalSeconds) return;

        Logger.Information(
            "[BattleTraffic] Movement {BytesPerSecond:0} wire bytes/s, {Deferred} deferred snapshot(s), " +
            "maximum deferred age {MaximumAge:0.000}s, {Available} budget bytes available",
            sentBytes / Math.Max(reportElapsed, 0.001f),
            maximumDeferredSnapshots,
            this.maximumDeferredAgeSeconds,
            AvailableBytes);

        reportElapsed = 0f;
        sentBytes = 0;
        maximumDeferredSnapshots = 0;
        this.maximumDeferredAgeSeconds = 0f;
    }

    public void Clear()
    {
        availableBytes = burstBytes;
        reportElapsed = 0f;
        sentBytes = 0;
        maximumDeferredSnapshots = 0;
        maximumDeferredAgeSeconds = 0f;
    }
}
