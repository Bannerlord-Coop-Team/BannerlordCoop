namespace Common.Network.Coalescing;

/// <summary>Controls when a coalesced update is sent relative to other pending updates.</summary>
public enum CoalescedSendPriority
{
    /// <summary>Sent before normal updates that may contain state derived from this update.</summary>
    Prerequisite,

    /// <summary>The default priority for independent updates.</summary>
    Normal,
}

/// <summary>
/// Buffers per-change network sends and collapses many updates to the same <see cref="CoalesceKey"/>
/// into one merged send per server tick. A send path enqueues instead of calling
/// <see cref="INetwork"/>.SendAll directly; the server flushes the buffer once per tick.
/// </summary>
/// <remarks>
/// Ordering obligations the consumers rely on:
/// <list type="bullet">
/// <item>The flush must run on the same thread that sends object creates and destroys, so a coalesced
/// update never reorders ahead of its object's create or after its destroy on the reliable-ordered
/// channel.</item>
/// <item>Before an object's destroy is sent, its pending updates must be sent (see
/// <see cref="FlushInstance"/>) or dropped (see <see cref="DropInstance"/>).</item>
/// </list>
/// </remarks>
public interface ISendCoalescer
{
    /// <summary>True when at least one update is buffered and awaiting a flush.</summary>
    bool HasPending { get; }

    /// <summary>
    /// Buffers an update for <paramref name="key"/>, merging it into any update already pending for that
    /// key via the payload's strategy.
    /// </summary>
    void Enqueue(
        CoalesceKey key,
        ICoalescedPayload payload,
        CoalescedSendPriority priority = CoalescedSendPriority.Normal);

    /// <summary>
    /// Broadcasts the merged message for every pending key and clears the buffer. Call once per server
    /// tick, on the thread that sends object creates and destroys.
    /// </summary>
    void Flush(INetwork network);

    /// <summary>
    /// Broadcasts and clears the pending updates for one instance only. Call before sending that
    /// instance's destroy so its final state reaches clients ahead of the destroy.
    /// </summary>
    void FlushInstance(string instanceId, INetwork network);

    /// <summary>Discards the pending updates for one instance without sending them.</summary>
    void DropInstance(string instanceId);
}
