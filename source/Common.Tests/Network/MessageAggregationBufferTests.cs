using Common.Network;

namespace Common.Tests.Network;

public class MessageAggregationBufferTests
{
    private static byte[] Payload(int size, byte fill = 0) => Enumerable.Repeat(fill, size).ToArray();

    [Fact]
    public void Append_BelowBudget_BuffersWithoutOverflow()
    {
        var buffer = new MessageAggregationBuffer(100);

        Assert.Null(buffer.Append(Payload(40)));
        Assert.Null(buffer.Append(Payload(40)));
        Assert.False(buffer.IsEmpty);
    }

    [Fact]
    public void Append_ExceedingBudget_ReturnsPreviousBatchAndStartsNext()
    {
        var buffer = new MessageAggregationBuffer(100);
        var first = Payload(60, fill: 1);
        var second = Payload(60, fill: 2);

        Assert.Null(buffer.Append(first));
        var overflow = buffer.Append(second);

        Assert.NotNull(overflow);
        Assert.Single(overflow);
        Assert.Same(first, overflow[0]);

        // The overflowing payload starts the next batch rather than joining the full one.
        var remaining = buffer.Drain();
        Assert.Single(remaining);
        Assert.Same(second, remaining[0]);
    }

    [Fact]
    public void Append_PreservesOrderWithinAndAcrossBatches()
    {
        var buffer = new MessageAggregationBuffer(100);
        var payloads = Enumerable.Range(0, 10).Select(i => Payload(30, fill: (byte)i)).ToList();

        var sent = new List<byte[]>();
        foreach (var payload in payloads)
        {
            var overflow = buffer.Append(payload);
            if (overflow != null) sent.AddRange(overflow);
        }
        var drained = buffer.Drain();
        if (drained != null) sent.AddRange(drained);

        Assert.Equal(payloads, sent);
    }

    [Fact]
    public void Append_FirstPayloadAlwaysAccepted_EvenAboveBudget()
    {
        // The send path routes oversized messages around the buffer, but the buffer itself must
        // never overflow-return an empty batch: the first payload is always accepted.
        var buffer = new MessageAggregationBuffer(100);

        Assert.Null(buffer.Append(Payload(150)));

        var drained = buffer.Drain();
        Assert.Single(drained);
    }

    [Fact]
    public void Drain_Empty_ReturnsNull()
    {
        var buffer = new MessageAggregationBuffer(100);

        Assert.True(buffer.IsEmpty);
        Assert.Null(buffer.Drain());
    }

    [Fact]
    public void Drain_ResetsBudgetForNextBatch()
    {
        var buffer = new MessageAggregationBuffer(100);

        buffer.Append(Payload(90));
        Assert.NotNull(buffer.Drain());
        Assert.True(buffer.IsEmpty);

        // A fresh batch gets the full budget again.
        Assert.Null(buffer.Append(Payload(90)));
    }
}
