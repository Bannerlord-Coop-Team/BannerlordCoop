using System.Collections.Generic;

namespace Common.Network;

/// <summary>
/// Accumulates serialized message payloads destined for one peer until they fill a byte budget,
/// so many small reliable messages leave as a few full packets instead of one packet each.
/// </summary>
/// <remarks>
/// LiteNetLib's reliable channel caps unacked packets in flight (not bytes), so per-peer throughput
/// is proportional to packet fullness; this buffer is what keeps world-sync bursts under that cap.
/// Not thread-safe by itself — the owner locks around each call (see <c>CoopNetworkBase</c>), which
/// also guarantees returned batches are sent in the order they were drained.
/// </remarks>
public class MessageAggregationBuffer
{
    private readonly int budgetBytes;

    private List<byte[]> payloads = new List<byte[]>();
    private int payloadBytes;

    /// <param name="budgetBytes">
    /// Combined payload size to aim for per batch. Chosen below the post-discovery MTU (~1.4KB) so a
    /// typical batch still fits one datagram after envelope framing.
    /// </param>
    public MessageAggregationBuffer(int budgetBytes)
    {
        this.budgetBytes = budgetBytes;
    }

    public bool IsEmpty => payloads.Count == 0;

    /// <summary>
    /// Buffers a payload. If accepting it would push the buffered total past the budget, the
    /// previously buffered payloads are returned to be sent now (the new payload starts the next
    /// batch); otherwise returns null.
    /// </summary>
    public List<byte[]> Append(byte[] payload)
    {
        List<byte[]> overflow = null;

        if (payloadBytes + payload.Length > budgetBytes && payloads.Count > 0)
        {
            overflow = TakeBatch();
        }

        payloads.Add(payload);
        payloadBytes += payload.Length;

        return overflow;
    }

    /// <summary>
    /// Removes and returns everything buffered, or null when there is nothing to send.
    /// </summary>
    public List<byte[]> Drain()
    {
        return payloads.Count == 0 ? null : TakeBatch();
    }

    private List<byte[]> TakeBatch()
    {
        var batch = payloads;
        payloads = new List<byte[]>();
        payloadBytes = 0;
        return batch;
    }
}
