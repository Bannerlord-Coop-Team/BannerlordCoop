using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;

namespace Common.Tests.Network;

/// <summary>Regression coverage for reliable message batching and ordering barriers.</summary>
public class ReliableMessageBatcherTests
{
    // Leaves 100 bytes of framed messages per batch.
    private const int MaxPacketBytes = 100 + ReliableMessageBatcher<string>.EnvelopeHeaderBytes;

    private readonly ICommonSerializer serializer =
        new ProtoBufSerializer(new SerializableTypeMapper());

    [Fact]
    public void Send_SubBudgetMessages_DoesNotSendUntilFlush()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var sent = new List<byte[]>();

        batcher.Send("peer", Payload(30, 1), MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Send("peer", Payload(30, 2), MaxPacketBytes, (_, data) => sent.Add(data));

        Assert.Empty(sent);

        batcher.Flush("peer", (_, data) => sent.Add(data));

        Assert.Single(sent);
    }

    [Fact]
    public void Flush_MultipleMessages_SendsOneAggregateInOriginalOrder()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var first = Payload(30, 1);
        var second = Payload(30, 2);
        var sent = new List<byte[]>();

        batcher.Send("peer", first, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Send("peer", second, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Flush("peer", (_, data) => sent.Add(data));

        var aggregate = Assert.IsType<AggregateMessagePacket>(
            serializer.Deserialize<IPacket>(Assert.Single(sent)));
        Assert.Equal(2, aggregate.Messages.Length);
        Assert.Equal(first, aggregate.Messages[0]);
        Assert.Equal(second, aggregate.Messages[1]);
    }

    [Fact]
    public void Flush_SingleMessage_SendsBarePayload()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var payload = Payload(30, 1);
        var sent = new List<byte[]>();

        batcher.Send("peer", payload, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Flush("peer", (_, data) => sent.Add(data));

        Assert.Same(payload, Assert.Single(sent));
    }

    [Fact]
    public void Send_Overflow_SendsPreviousBatchBeforeOverflowingMessage()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var first = Payload(60, 1);
        var second = Payload(60, 2);
        var sent = new List<byte[]>();

        batcher.Send("peer", first, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Send("peer", second, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Flush("peer", (_, data) => sent.Add(data));

        Assert.Equal(2, sent.Count);
        Assert.Same(first, sent[0]);
        Assert.Same(second, sent[1]);
    }

    [Fact]
    public void Send_OversizedMessage_FlushesEarlierMessagesBeforeBarePayload()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var buffered = Payload(30, 1);
        var oversized = Payload(100, 2);
        var sent = new List<byte[]>();

        batcher.Send("peer", buffered, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Send("peer", oversized, MaxPacketBytes, (_, data) => sent.Add(data));

        Assert.Equal(2, sent.Count);
        Assert.Same(buffered, sent[0]);
        Assert.Same(oversized, sent[1]);
    }

    [Fact]
    public void SendImmediate_FlushesEarlierMessagesBeforeImmediatePayload()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var first = Payload(30, 1);
        var second = Payload(30, 2);
        var immediate = Payload(10, 3);
        var sent = new List<byte[]>();

        batcher.Send("peer", first, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.Send("peer", second, MaxPacketBytes, (_, data) => sent.Add(data));
        batcher.SendImmediate("peer", immediate, (_, data) => sent.Add(data));

        Assert.Equal(2, sent.Count);
        var aggregate = Assert.IsType<AggregateMessagePacket>(
            serializer.Deserialize<IPacket>(sent[0]));
        Assert.Equal(first, aggregate.Messages[0]);
        Assert.Equal(second, aggregate.Messages[1]);
        Assert.Same(immediate, sent[1]);
    }

    [Fact]
    public void FlushThen_SendsBufferedMessagesBeforeBarrier()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var order = new List<string>();

        batcher.Send("peer", Payload(30), MaxPacketBytes, (_, _) => order.Add("messages"));
        batcher.FlushThen(
            "peer",
            (_, _) => order.Add("messages"),
            () => order.Add("barrier"));

        Assert.Equal(new[] { "messages", "barrier" }, order);
    }

    [Fact]
    public void FlushAll_PrunesDisconnectedDestination()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var sentDestinations = new List<string>();

        batcher.Send("connected", Payload(30), MaxPacketBytes, (_, _) => { });
        batcher.Send("disconnected", Payload(30), MaxPacketBytes, (_, _) => { });
        batcher.FlushAll(
            destination => destination == "connected",
            (destination, _) => sentDestinations.Add(destination));
        batcher.Flush(
            "disconnected",
            (destination, _) => sentDestinations.Add(destination));

        Assert.Equal(new[] { "connected" }, sentDestinations);
    }

    [Fact]
    public void Send_NoAggregateExceedsTheRouteSinglePacketSize()
    {
        // LiteNetLib's reliable single-packet size at its initial 1024-byte MTU.
        const int maxPacketBytes = 1020;
        var batcher = new ReliableMessageBatcher<string>(serializer);
        var random = new Random(4488);
        List<byte[]> payloads = Enumerable.Range(0, 2000)
            .Select(i => Payload(random.Next(1, 600), (byte)i))
            .ToList();
        var bare = new HashSet<byte[]>(payloads);
        var sent = new List<byte[]>();

        foreach (byte[] payload in payloads)
            batcher.Send("peer", payload, maxPacketBytes, (_, data) => sent.Add(data));
        batcher.Flush("peer", (_, data) => sent.Add(data));

        var received = new List<byte[]>();
        foreach (byte[] data in sent)
        {
            if (bare.Contains(data))
            {
                received.Add(data);
                continue;
            }

            Assert.InRange(data.Length, 1, maxPacketBytes);
            received.AddRange(Assert.IsType<AggregateMessagePacket>(serializer.Deserialize<IPacket>(data)).Messages);
        }
        Assert.Equal(payloads, received);
        Assert.True(sent.Count < payloads.Count / 2);
    }

    private static byte[] Payload(int size, byte fill = 0)
    {
        return Enumerable.Repeat(fill, size).ToArray();
    }
}
