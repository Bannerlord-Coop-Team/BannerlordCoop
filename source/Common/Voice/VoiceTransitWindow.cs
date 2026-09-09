using System.Collections.Generic;
using System.Linq;

namespace Common.Voice;

public interface IVoiceTransitWindow
{
    bool Accept(long sentAt, long receivedAt);
}

/// <summary>Relative transit age without synchronized clocks; a rolling baseline accommodates clock drift.</summary>
public sealed class VoiceTransitWindow : IVoiceTransitWindow
{
    private readonly Queue<long> offsets = new();

    public bool Accept(long sentAt, long receivedAt)
    {
        if (sentAt <= 0 || receivedAt < 0) return false;
        long offset = receivedAt - sentAt;
        offsets.Enqueue(offset);
        if (offsets.Count > 100) offsets.Dequeue();
        long baseline = offsets.Min();
        return offset - baseline <= VoiceJitterBuffer.MaximumAgeMilliseconds;
    }
}
