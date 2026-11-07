using System.Collections.Generic;

namespace Common.Network;

/// <summary>
/// Accumulates serialized message payloads for one destination until they fill a byte budget,
/// so many small reliable messages leave as a few full packets instead of one packet each.
/// </summary>
/// <remarks>
/// LiteNetLib's reliable channel caps unacked packets in flight (not bytes), so per-peer throughput
/// is proportional to packet fullness; this buffer is what keeps world-sync bursts under that cap.
/// Not thread-safe by itself — the owner locks around each call (see
/// <see cref="ReliableMessageBatcher{TDestination}"/>), which
/// also guarantees returned batches are sent in the order they were drained.
/// </remarks>
public class MessageAggregationBuffer
{
    private List<byte[]> payloads = new List<byte[]>();
    private int framedBytes;

    public bool IsEmpty => payloads.Count == 0;

    /// <summary>
    /// Bytes one payload takes inside <see cref="Common.PacketHandlers.AggregateMessagePacket.Messages"/>:
    /// the field tag, the varint length and the payload itself.
    /// </summary>
    public static int GetFramedLength(byte[] payload)
    {
        int length = payload.Length;
        int lengthBytes = 1;
        while ((length >>= 7) != 0) lengthBytes++;
        return 1 + lengthBytes + payload.Length;
    }

    /// <summary>
    /// Buffers a payload. Returns the earlier batch to send now when the payload's framed bytes would push it past
    /// <paramref name="maxFramedBytes"/> (the new payload starts the next batch); otherwise returns null.
    /// </summary>
    public List<byte[]> Append(byte[] payload, int maxFramedBytes)
    {
        List<byte[]> overflow = null;
        int payloadFramedBytes = GetFramedLength(payload);

        if (framedBytes + payloadFramedBytes > maxFramedBytes && payloads.Count > 0)
        {
            overflow = TakeBatch();
        }

        payloads.Add(payload);
        framedBytes += payloadFramedBytes;

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
        framedBytes = 0;
        return batch;
    }
}
