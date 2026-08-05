using System;

namespace Common.Util;

/// <summary>
/// What a guard should write for a fault it just suppressed.
/// </summary>
public enum FaultLogAction
{
    /// <summary>A fault not seen last time — log it in full, with the exception.</summary>
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
/// Worth having on any per-frame or per-packet guard: the log is a fixed-size sink that drops its
/// middle when full, so an unthrottled repeat evicts the window holding the first occurrence — the
/// one worth reading. Faults are matched by exception message, which is coarse (two different
/// null-reference sites collapse into one) but needs no allocation and no stack comparison. One
/// instance per guard site; not thread-safe, so give each caller its own.
/// </remarks>
public sealed class FaultLogThrottle
{
    /// <summary>Matches the cadence Poller used before this type existed.</summary>
    public const long DefaultRepeatInterval = 1000;

    private readonly long repeatInterval;
    private string lastFault;
    private long repeatCount;

    public FaultLogThrottle(long repeatInterval = DefaultRepeatInterval)
    {
        if (repeatInterval <= 0) throw new ArgumentOutOfRangeException(nameof(repeatInterval));
        this.repeatInterval = repeatInterval;
    }

    /// <summary>
    /// Records <paramref name="fault"/> and says what to log for it. <paramref name="repeats"/> carries
    /// how many times it has repeated, for the <see cref="FaultLogAction.Summary"/> case.
    /// </summary>
    public FaultLogAction Classify(Exception fault, out long repeats)
    {
        string message = fault?.Message;
        repeats = 0;

        if (message != lastFault)
        {
            lastFault = message;
            repeatCount = 0;
            return FaultLogAction.Full;
        }

        repeats = ++repeatCount;
        return repeats % repeatInterval == 0 ? FaultLogAction.Summary : FaultLogAction.Suppress;
    }
}
