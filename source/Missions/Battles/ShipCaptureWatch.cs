#if DEBUG
using Common.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;

namespace Missions.Battles;

/// <summary>
/// Logs from a timer thread, for a while after each capture applied here, that the process is alive and when the game thread
/// last ticked the naval services: a freeze after a capture then shows whether the game thread or the whole process stopped.
/// </summary>
public interface IShipCaptureWatch : IDisposable
{
    /// <summary>[Game thread] Starts, or restarts, the watch for the capture of <paramref name="shipId"/> just applied.</summary>
    void Start(Guid shipId);

    /// <summary>[Game thread] Notes one tick of the naval services.</summary>
    void NoteTick();
}

/// <inheritdoc cref="IShipCaptureWatch"/>
public class ShipCaptureWatch : IShipCaptureWatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ShipCaptureWatch>();

    internal const double WatchSeconds = 30;
    internal const int IntervalMilliseconds = 2000;

    private readonly object gate = new object();
    private Timer timer;
    private Guid shipId;
    private long startedAt;
    private long ticks;
    private long ticksAtLastLine;
    private long lastTickAt;
    private bool running;

    /// <summary>Whether the alive lines of a capture are still being written.</summary>
    internal bool IsRunning { get { lock (gate) return running; } }

    // A capture restarts the watch, so the lines always follow the latest one.
    public void Start(Guid capturedShipId)
    {
        lock (gate)
        {
            shipId = capturedShipId;
            startedAt = Stopwatch.GetTimestamp();
            ticksAtLastLine = Interlocked.Read(ref ticks);
            timer ??= new Timer(_ => Report(), null, Timeout.Infinite, Timeout.Infinite);
            timer.Change(IntervalMilliseconds, IntervalMilliseconds);
            running = true;
        }
    }

    // Read on the timer thread, so both counters are atomic.
    public void NoteTick()
    {
        Interlocked.Increment(ref ticks);
        Interlocked.Exchange(ref lastTickAt, Stopwatch.GetTimestamp());
    }

    // [Timer thread] One alive line; the last one, after the watch ran out, stops the timer.
    private void Report()
    {
        lock (gate)
        {
            if (timer == null) return;

            long now = Stopwatch.GetTimestamp();
            double sinceCapture = SecondsBetween(startedAt, now);
            long total = Interlocked.Read(ref ticks);
            long last = Interlocked.Read(ref lastTickAt);
            Logger.Information("[NavalSync] Capture watch: {Seconds:0.0} s after hull {ShipId} was captured; {Ticks} naval tick(s) since " +
                "the last line, the last {Age:0.0} s ago", sinceCapture, shipId, total - ticksAtLastLine, SecondsBetween(last, now));
            ticksAtLastLine = total;
            if (!IsOver(sinceCapture)) return;

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            running = false;
        }
    }

    /// <summary>Whether the watch of a capture <paramref name="secondsSinceCapture"/> ago has run out.</summary>
    internal static bool IsOver(double secondsSinceCapture) => secondsSinceCapture >= WatchSeconds;

    // Seconds between two Stopwatch timestamps.
    private static double SecondsBetween(long from, long to) => (double)(to - from) / Stopwatch.Frequency;

    // Stops the timer; a line already running finishes first.
    public void Dispose()
    {
        lock (gate)
        {
            timer?.Dispose();
            timer = null;
            running = false;
        }
    }
}
#endif
