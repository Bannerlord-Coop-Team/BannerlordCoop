using Common.Messaging;

namespace Common.Network.Coalescing;

/// <summary>
/// A pending coalesced update together with the strategy for merging successive updates to the same
/// <see cref="CoalesceKey"/> within a server tick. Implementations decide how two updates combine
/// (<see cref="Merge"/>) and how the final pending value becomes a wire message (<see cref="ToMessage"/>).
/// </summary>
public interface ICoalescedPayload
{
    /// <summary>
    /// Combines this payload, the value already pending for the key, with a newer one and returns the
    /// merged result. Called on every re-enqueue of an already-pending key. Implementations require
    /// <paramref name="incoming"/> to be the same payload type, since one coalesce key uses a single
    /// strategy for its whole life.
    /// </summary>
    ICoalescedPayload Merge(ICoalescedPayload incoming);

    /// <summary>
    /// Builds the message to broadcast for the final merged value. Invoked once per key at flush time,
    /// so a snapshot payload can defer reading live state until here.
    /// </summary>
    IMessage ToMessage();
}
