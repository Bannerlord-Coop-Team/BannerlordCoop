using Common.PacketHandlers;
using Common.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Common.Network;

/// <summary>Batches reliable messages independently for each logical destination.</summary>
public interface IReliableMessageBatcher<TDestination> where TDestination : class
{
    event Action<AggregateMessagePacket, int> AggregateSent;
    /// <param name="maxPacketBytes">
    /// Largest reliable payload one datagram carries on the destination's current route; batches stay within it.
    /// </param>
    void Send(
        TDestination destination,
        byte[] messagePayload,
        int maxPacketBytes,
        Action<TDestination, byte[]> sendReliableOrdered);
    void SendImmediate(
        TDestination destination,
        byte[] messagePayload,
        Action<TDestination, byte[]> sendReliableOrdered);
    void Flush(
        TDestination destination,
        Action<TDestination, byte[]> sendReliableOrdered);
    void FlushThen(
        TDestination destination,
        Action<TDestination, byte[]> sendReliableOrdered,
        Action sendAfterFlush);
    void FlushAll(
        Func<TDestination, bool> isConnected,
        Action<TDestination, byte[]> sendReliableOrdered);
    void Remove(TDestination destination);
    void Clear();
}

/// <summary>
/// Batches small serialized messages per destination so LiteNetLib's fixed reliable packet window
/// carries full datagrams instead of one small message per slot.
/// </summary>
public class ReliableMessageBatcher<TDestination> : IReliableMessageBatcher<TDestination>
    where TDestination : class
{
    /// <summary>
    /// Upper bound on the <see cref="ProtoBufSerializer"/> wrapper around an aggregate's messages field: two tags,
    /// the type id and the length, each varint at most 5 bytes.
    /// </summary>
    internal const int EnvelopeHeaderBytes = 12;

    private sealed class DestinationBuffer
    {
        public readonly object Lock = new object();
        public readonly MessageAggregationBuffer Buffer = new MessageAggregationBuffer();
    }

    private readonly ICommonSerializer serializer;
    private readonly ConcurrentDictionary<TDestination, DestinationBuffer> pendingMessages =
        new ConcurrentDictionary<TDestination, DestinationBuffer>();

    public event Action<AggregateMessagePacket, int> AggregateSent;

    public ReliableMessageBatcher(ICommonSerializer serializer)
    {
        if (serializer == null) throw new ArgumentNullException(nameof(serializer));

        this.serializer = serializer;
    }

    public void Send(
        TDestination destination,
        byte[] messagePayload,
        int maxPacketBytes,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        ValidateSend(destination, messagePayload, sendReliableOrdered);

        int maxFramedBytes = maxPacketBytes - EnvelopeHeaderBytes;
        DestinationBuffer destinationBuffer = GetBuffer(destination);
        lock (destinationBuffer.Lock)
        {
            // Too big for an envelope on this route, so it goes bare after whatever is already buffered.
            if (MessageAggregationBuffer.GetFramedLength(messagePayload) > maxFramedBytes)
            {
                SendDrainedBatch(
                    destination,
                    destinationBuffer.Buffer.Drain(),
                    sendReliableOrdered);
                TrafficProbe4488.Record("reliable:bare", messagePayload.Length); // TEMP #4488
                sendReliableOrdered(destination, messagePayload);
                return;
            }

            SendDrainedBatch(
                destination,
                destinationBuffer.Buffer.Append(messagePayload, maxFramedBytes),
                sendReliableOrdered);
        }
    }

    public void SendImmediate(
        TDestination destination,
        byte[] messagePayload,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        ValidateSend(destination, messagePayload, sendReliableOrdered);

        DestinationBuffer destinationBuffer = GetBuffer(destination);
        lock (destinationBuffer.Lock)
        {
            SendDrainedBatch(
                destination,
                destinationBuffer.Buffer.Drain(),
                sendReliableOrdered);
            TrafficProbe4488.Record("reliable:immediate", messagePayload.Length); // TEMP #4488
            sendReliableOrdered(destination, messagePayload);
        }
    }

    public void Flush(
        TDestination destination,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (sendReliableOrdered == null) throw new ArgumentNullException(nameof(sendReliableOrdered));
        if (!pendingMessages.TryGetValue(destination, out DestinationBuffer destinationBuffer)) return;

        lock (destinationBuffer.Lock)
        {
            SendDrainedBatch(
                destination,
                destinationBuffer.Buffer.Drain(),
                sendReliableOrdered);
        }
    }

    public void FlushThen(
        TDestination destination,
        Action<TDestination, byte[]> sendReliableOrdered,
        Action sendAfterFlush)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (sendReliableOrdered == null) throw new ArgumentNullException(nameof(sendReliableOrdered));
        if (sendAfterFlush == null) throw new ArgumentNullException(nameof(sendAfterFlush));

        DestinationBuffer destinationBuffer = GetBuffer(destination);
        lock (destinationBuffer.Lock)
        {
            SendDrainedBatch(
                destination,
                destinationBuffer.Buffer.Drain(),
                sendReliableOrdered);
            sendAfterFlush();
        }
    }

    public void FlushAll(
        Func<TDestination, bool> isConnected,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        if (isConnected == null) throw new ArgumentNullException(nameof(isConnected));
        if (sendReliableOrdered == null) throw new ArgumentNullException(nameof(sendReliableOrdered));

        foreach (KeyValuePair<TDestination, DestinationBuffer> entry in pendingMessages)
        {
            if (!isConnected(entry.Key))
            {
                Remove(entry.Key);
                continue;
            }

            Flush(entry.Key, sendReliableOrdered);
        }
    }

    public void Remove(TDestination destination)
    {
        if (destination == null) return;
        if (!pendingMessages.TryRemove(destination, out DestinationBuffer destinationBuffer)) return;

        lock (destinationBuffer.Lock)
        {
            destinationBuffer.Buffer.Drain();
        }
    }

    public void Clear()
    {
        foreach (TDestination destination in pendingMessages.Keys)
        {
            Remove(destination);
        }
    }

    private static void ValidateSend(
        TDestination destination,
        byte[] messagePayload,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (messagePayload == null) throw new ArgumentNullException(nameof(messagePayload));
        if (sendReliableOrdered == null) throw new ArgumentNullException(nameof(sendReliableOrdered));
    }

    private DestinationBuffer GetBuffer(TDestination destination)
    {
        return pendingMessages.GetOrAdd(
            destination,
            _ => new DestinationBuffer());
    }

    private void SendDrainedBatch(
        TDestination destination,
        List<byte[]> payloads,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        if (payloads == null) return;

        if (payloads.Count == 1)
        {
            TrafficProbe4488.Record("reliable:single", payloads[0].Length); // TEMP #4488
            sendReliableOrdered(destination, payloads[0]);
            return;
        }

        var envelope = new AggregateMessagePacket(payloads.ToArray());
        byte[] data = serializer.Serialize(envelope);
        TrafficProbe4488.Record("reliable:aggregate", data.Length); // TEMP #4488
        sendReliableOrdered(destination, data);

        if (AggregateSent == null) return;

        int framingOverhead = data.Length;
        foreach (byte[] payload in payloads)
        {
            framingOverhead -= payload.Length;
        }
        AggregateSent(envelope, framingOverhead);
    }
}
