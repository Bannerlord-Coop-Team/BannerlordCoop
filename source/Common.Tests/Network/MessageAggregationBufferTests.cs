using Common.Network;

namespace Common.Tests.Network;

public class MessageAggregationBufferTests
{
    private const int Budget = 100;

    private static byte[] Payload(int size, byte fill = 0) => Enumerable.Repeat(fill, size).ToArray();

    [Fact]
    public void Append_BelowBudget_BuffersWithoutOverflow()
    {
        var buffer = new MessageAggregationBuffer();

        Assert.Null(buffer.Append(Payload(40), Budget));
        Assert.Null(buffer.Append(Payload(40), Budget));
        Assert.Equal(2, buffer.Drain().Count);
    }

    [Fact]
    public void Append_ExceedingBudget_ReturnsPreviousBatchAndStartsNext()
    {
        var buffer = new MessageAggregationBuffer();
        var first = Payload(60, fill: 1);
        var second = Payload(60, fill: 2);

        Assert.Null(buffer.Append(first, Budget));
        var overflow = buffer.Append(second, Budget);

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
        var buffer = new MessageAggregationBuffer();
        var payloads = Enumerable.Range(0, 10).Select(i => Payload(30, fill: (byte)i)).ToList();

        var sent = new List<byte[]>();
        foreach (var payload in payloads)
        {
            var overflow = buffer.Append(payload, Budget);
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
        var buffer = new MessageAggregationBuffer();

        Assert.Null(buffer.Append(Payload(150), Budget));

        var drained = buffer.Drain();
        Assert.Single(drained);
    }

    [Fact]
    public void Drain_Empty_ReturnsNull()
    {
        var buffer = new MessageAggregationBuffer();

        Assert.Null(buffer.Drain());
    }

    [Fact]
    public void Drain_ResetsBudgetForNextBatch()
    {
        var buffer = new MessageAggregationBuffer();

        buffer.Append(Payload(90), Budget);
        Assert.NotNull(buffer.Drain());

        // A fresh batch gets the full budget again.
        Assert.Null(buffer.Append(Payload(90), Budget));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(127, 129)]
    [InlineData(128, 131)]
    [InlineData(1000, 1003)]
    public void GetFramedLength_AddsTagAndVarintLength(int payloadBytes, int framedBytes)
    {
        Assert.Equal(framedBytes, MessageAggregationBuffer.GetFramedLength(Payload(payloadBytes)));
    }

    [Fact]
    public void Append_CountsFramingTowardTheBudget()
    {
        var buffer = new MessageAggregationBuffer();
        var first = Payload(49, fill: 1);

        // 98 raw bytes would fit, but each payload takes 51 bytes once framed in the envelope.
        Assert.Null(buffer.Append(first, Budget));
        Assert.Same(first, Assert.Single(buffer.Append(Payload(49, fill: 2), Budget)));
    }
}
