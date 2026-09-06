using Common.PacketHandlers;
using Common.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Common.Network;

/// <summary>Batches reliable messages independently for each logical destination.</summary>
public interface IReliableMessageBatcher<TDestination> where TDestination : class
{
    int BudgetBytes { get; }
    event Action<AggregateMessagePacket, int> AggregateSent;
    void Send(
        TDestination destination,
        byte[] messagePayload,
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
    /// Leaves room for the aggregate envelope and LiteNetLib framing within its discovered MTU.
    /// </summary>
    public const int DefaultBudgetBytes = 1200;

    private sealed class DestinationBuffer
    {
        public readonly object Lock = new object();
        public readonly MessageAggregationBuffer Buffer;

        public DestinationBuffer(int budgetBytes)
        {
            Buffer = new MessageAggregationBuffer(budgetBytes);
        }
    }

    private readonly ICommonSerializer serializer;
    private readonly ConcurrentDictionary<TDestination, DestinationBuffer> pendingMessages =
        new ConcurrentDictionary<TDestination, DestinationBuffer>();

    public int BudgetBytes { get; }
    public event Action<AggregateMessagePacket, int> AggregateSent;

    public ReliableMessageBatcher(ICommonSerializer serializer)
        : this(serializer, DefaultBudgetBytes)
    {
    }

    public ReliableMessageBatcher(ICommonSerializer serializer, int budgetBytes)
    {
        if (serializer == null) throw new ArgumentNullException(nameof(serializer));
        if (budgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));

        this.serializer = serializer;
        BudgetBytes = budgetBytes;
    }

    public void Send(
        TDestination destination,
        byte[] messagePayload,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        ValidateSend(destination, messagePayload, sendReliableOrdered);

        DestinationBuffer destinationBuffer = GetBuffer(destination);
        lock (destinationBuffer.Lock)
        {
            if (messagePayload.Length >= BudgetBytes)
            {
                SendDrainedBatch(
                    destination,
                    destinationBuffer.Buffer.Drain(),
                    sendReliableOrdered);
                sendReliableOrdered(destination, messagePayload);
                return;
            }

            SendDrainedBatch(
                destination,
                destinationBuffer.Buffer.Append(messagePayload),
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
            _ => new DestinationBuffer(BudgetBytes));
    }

    private void SendDrainedBatch(
        TDestination destination,
        List<byte[]> payloads,
        Action<TDestination, byte[]> sendReliableOrdered)
    {
        if (payloads == null) return;

        if (payloads.Count == 1)
        {
            sendReliableOrdered(destination, payloads[0]);
            return;
        }

        var envelope = new AggregateMessagePacket(payloads.ToArray());
        byte[] data = serializer.Serialize(envelope);
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
