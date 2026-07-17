using Common.Logging;
using Common.PacketHandlers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Common.Tests.Logging;

/// <summary>Tests the packet profiler accumulator and allocation-free profile keys.</summary>
public class PacketProfileAccumulatorTests
{
    [Fact]
    public void Record_AccumulatesPacketCountAndBytes()
    {
        var accumulator = new PacketProfileAccumulator();
        var key = new PacketProfileKey(typeof(TestPacket), null);

        accumulator.Record(key, 10);
        accumulator.Record(key, 15);

        var snapshot = accumulator.Drain();
        var stats = Assert.Single(snapshot).Value;
        Assert.Equal(2, stats.PacketsSent);
        Assert.Equal(25, stats.BytesSent);
        Assert.Null(accumulator.Drain());
    }

    [Fact]
    public void Drain_SeparatesReportingWindows()
    {
        var accumulator = new PacketProfileAccumulator();
        var key = new PacketProfileKey(typeof(TestPacket), null);

        accumulator.Record(key, 10);
        var first = accumulator.Drain();
        accumulator.Record(key, 20);
        var second = accumulator.Drain();

        Assert.Equal(10, Assert.Single(first).Value.BytesSent);
        Assert.Equal(20, Assert.Single(second).Value.BytesSent);
    }

    [Fact]
    public void Record_IsExactUnderConcurrentWriters()
    {
        const int Records = 10_000;
        var accumulator = new PacketProfileAccumulator();
        var key = new PacketProfileKey(typeof(TestPacket), null);

        Parallel.For(0, Records, _ => accumulator.Record(key, 3));

        var stats = Assert.Single(accumulator.Drain()).Value;
        Assert.Equal(Records, stats.PacketsSent);
        Assert.Equal(Records * 3, stats.BytesSent);
    }

    [Fact]
    public void Record_AfterWarmupAllocatesNoManagedMemory()
    {
        const int Records = 1_000;
        var accumulator = new PacketProfileAccumulator();
        var key = new PacketProfileKey(typeof(TestPacket), null);

        accumulator.Record(key, 1);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < Records; i++)
        {
            accumulator.Record(key, 1);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    public void Name_PreservesMessagePacketGenericTypeFormatting()
    {
        var key = new PacketProfileKey(
            typeof(MessagePacket),
            typeof(TestMessage<Dictionary<string, int>>));

        Assert.Equal("MessagePacket:TestMessage<Dictionary<String, Int32>>", key.Name);
    }

    /// <summary>Packet type used to exercise profiler keys.</summary>
    private sealed class TestPacket
    {
    }

    /// <summary>Generic message type used to verify friendly names.</summary>
    private sealed class TestMessage<T>
    {
    }
}
