using System.Diagnostics;

namespace Common.Voice;

public interface IVoiceClock
{
    long Milliseconds { get; }
}

public sealed class VoiceClock : IVoiceClock
{
    public long Milliseconds => (long)(Stopwatch.GetTimestamp() * (1000d / Stopwatch.Frequency));
}
