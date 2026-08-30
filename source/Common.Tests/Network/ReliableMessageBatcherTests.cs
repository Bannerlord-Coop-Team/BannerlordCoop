using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;

namespace Common.Tests.Network;

/// <summary>Regression coverage for reliable message batching and ordering barriers.</summary>
public class ReliableMessageBatcherTests
{
    private readonly ICommonSerializer serializer =
        new ProtoBufSerializer(new SerializableTypeMapper());

    [Fact]
    public void Send_SubBudgetMessages_DoesNotSendUntilFlush()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var sent = new List<byte[]>();

        batcher.Send("peer", Payload(30, 1), (_, data) => sent.Add(data));
        batcher.Send("peer", Payload(30, 2), (_, data) => sent.Add(data));

        Assert.Empty(sent);

        batcher.Flush("peer", (_, data) => sent.Add(data));

        Assert.Single(sent);
    }

    [Fact]
    public void Flush_MultipleMessages_SendsOneAggregateInOriginalOrder()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var first = Payload(30, 1);
        var second = Payload(30, 2);
        var sent = new List<byte[]>();

        batcher.Send("peer", first, (_, data) => sent.Add(data));
        batcher.Send("peer", second, (_, data) => sent.Add(data));
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
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var payload = Payload(30, 1);
        var sent = new List<byte[]>();

        batcher.Send("peer", payload, (_, data) => sent.Add(data));
        batcher.Flush("peer", (_, data) => sent.Add(data));

        Assert.Same(payload, Assert.Single(sent));
    }

    [Fact]
    public void Send_Overflow_SendsPreviousBatchBeforeOverflowingMessage()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var first = Payload(60, 1);
        var second = Payload(60, 2);
        var sent = new List<byte[]>();

        batcher.Send("peer", first, (_, data) => sent.Add(data));
        batcher.Send("peer", second, (_, data) => sent.Add(data));
        batcher.Flush("peer", (_, data) => sent.Add(data));

        Assert.Equal(2, sent.Count);
        Assert.Same(first, sent[0]);
        Assert.Same(second, sent[1]);
    }

    [Fact]
    public void Send_OversizedMessage_FlushesEarlierMessagesBeforeBarePayload()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var buffered = Payload(30, 1);
        var oversized = Payload(100, 2);
        var sent = new List<byte[]>();

        batcher.Send("peer", buffered, (_, data) => sent.Add(data));
        batcher.Send("peer", oversized, (_, data) => sent.Add(data));

        Assert.Equal(2, sent.Count);
        Assert.Same(buffered, sent[0]);
        Assert.Same(oversized, sent[1]);
    }

    [Fact]
    public void SendImmediate_FlushesEarlierMessagesBeforeImmediatePayload()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var first = Payload(30, 1);
        var second = Payload(30, 2);
        var immediate = Payload(10, 3);
        var sent = new List<byte[]>();

        batcher.Send("peer", first, (_, data) => sent.Add(data));
        batcher.Send("peer", second, (_, data) => sent.Add(data));
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
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var order = new List<string>();

        batcher.Send("peer", Payload(30), (_, _) => order.Add("messages"));
        batcher.FlushThen(
            "peer",
            (_, _) => order.Add("messages"),
            () => order.Add("barrier"));

        Assert.Equal(new[] { "messages", "barrier" }, order);
    }

    [Fact]
    public void FlushAll_PrunesDisconnectedDestination()
    {
        var batcher = new ReliableMessageBatcher<string>(serializer, 100);
        var sentDestinations = new List<string>();

        batcher.Send("connected", Payload(30), (_, _) => { });
        batcher.Send("disconnected", Payload(30), (_, _) => { });
        batcher.FlushAll(
            destination => destination == "connected",
            (destination, _) => sentDestinations.Add(destination));
        batcher.Flush(
            "disconnected",
            (destination, _) => sentDestinations.Add(destination));

        Assert.Equal(new[] { "connected" }, sentDestinations);
    }

    private static byte[] Payload(int size, byte fill = 0)
    {
        return Enumerable.Repeat(fill, size).ToArray();
    }
}
