using System;
using System.Collections.Generic;

namespace Common.Util;

/// <summary>
/// What a guard should write for a fault it just suppressed.
/// </summary>
public enum FaultLogAction
{
    /// <summary>A fault not seen before — log it in full, with the exception.</summary>
    Full,

    /// <summary>The same fault again, and a periodic summary is due — log a count, without the exception.</summary>
    Summary,

    /// <summary>The same fault again — stay quiet.</summary>
    Suppress,
}

/// <summary>
/// Keeps a guard that swallows and continues from flooding the log when the same fault recurs every
/// tick. Extracted from <see cref="Poller"/>, which needed exactly this and whose loop now uses it.
/// </summary>
/// <remarks>
/// Worth having on any per-frame or per-packet guard. The log sink drops its middle when full, so an
/// unthrottled repeat evicts the first occurrence, the one worth reading. A fault is its source paired
/// with the exception message and each is counted on its own, so a noisy source neither hides another's
/// fault nor collects its count. <see cref="DefaultCapacity"/> bounds the tracked set because a message
/// can carry an id, and overflow forgets everything, costing one more full report per live fault.
/// </remarks>
public sealed class FaultLogThrottle
{
    /// <summary>Matches the cadence Poller used before this type existed.</summary>
    public const long DefaultRepeatInterval = 1000;

    /// <summary>How many distinct faults are tracked before the throttle forgets and starts over.</summary>
    public const int DefaultCapacity = 256;

    private readonly long repeatInterval;
    private readonly int capacity;
    private readonly Dictionary<(string Source, string Fault), long> repeatsByFault =
        new Dictionary<(string Source, string Fault), long>();

    // Only reached from a guard that already caught something, so the lock is never on a hot path.
    private readonly object gate = new object();

    public FaultLogThrottle(long repeatInterval = DefaultRepeatInterval, int capacity = DefaultCapacity)
    {
        if (repeatInterval <= 0) throw new ArgumentOutOfRangeException(nameof(repeatInterval));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.repeatInterval = repeatInterval;
        this.capacity = capacity;
    }

    /// <summary>For a guard with only one source.</summary>
    public FaultLogAction Classify(Exception fault, out long repeats) =>
        Classify(null, fault, out repeats);

    /// <summary>
    /// Records <paramref name="fault"/> against <paramref name="source"/> — the updateable, peer or queued
    /// action it came from — and says what to log. <paramref name="repeats"/> is that pair's running count.
    /// </summary>
    public FaultLogAction Classify(string source, Exception fault, out long repeats) =>
        Classify(source, fault?.Message, out repeats);

    /// <summary>
    /// For a fault with no exception behind it, <paramref name="fault"/> being what tells it apart from the
    /// other faults of <paramref name="source"/>.
    /// </summary>
    public FaultLogAction Classify(string source, string fault, out long repeats)
    {
        var key = (Source: source, Fault: fault);

        lock (gate)
        {
            if (!repeatsByFault.TryGetValue(key, out long seen))
            {
                if (repeatsByFault.Count >= capacity) repeatsByFault.Clear();

                repeatsByFault[key] = 0;
                repeats = 0;
                return FaultLogAction.Full;
            }

            repeats = seen + 1;
            repeatsByFault[key] = repeats;
            return repeats % repeatInterval == 0 ? FaultLogAction.Summary : FaultLogAction.Suppress;
        }
    }
}
