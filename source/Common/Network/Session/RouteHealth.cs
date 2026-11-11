namespace Common.Network.Session;

/// <summary>One reading of a peer route's delivery health, taken from its transport's own counters.</summary>
public readonly struct RouteHealth
{
    /// <summary>Quality of a route whose transport has no delivery measurement, matching Steam's -1.</summary>
    public const float UnknownQuality = -1f;

    public int RoundTripMilliseconds { get; }
    public float Quality { get; }
    public long QueuedBytes { get; }

    public RouteHealth(int roundTripMilliseconds, float quality, long queuedBytes)
    {
        RoundTripMilliseconds = roundTripMilliseconds;
        Quality = quality;
        QueuedBytes = queuedBytes;
    }
}
