using System;
using System.Collections.Generic;
using Xunit;

namespace E2E.Tests.Environment;

public class VirtualNetworkSchedulerTests
{
    [Fact]
    public void AdvanceBy_DeliversOnlyWhenLatencyExpires()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(50),
        };
        var sender = new object();
        var receiver = new object();
        int deliveries = 0;

        scheduler.Schedule(sender, receiver, "message", () => deliveries++);

        Assert.Equal(0, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(49)));
        Assert.Equal(0, deliveries);
        Assert.Equal(1, scheduler.PendingDeliveryCount);

        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1)));
        Assert.Equal(1, deliveries);
        Assert.Equal(0, scheduler.PendingDeliveryCount);
    }

    [Theory]
    [InlineData("message")]
    [InlineData("packet:Unreliable")]
    public void ReducedLatency_DoesNotReorderOneDirectedChannel(string channel)
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(100),
        };
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<int>();

        scheduler.Schedule(sender, receiver, channel, () => deliveries.Add(1));
        scheduler.DefaultLatency = TimeSpan.Zero;
        scheduler.Schedule(sender, receiver, channel, () => deliveries.Add(2));

        Assert.Empty(deliveries);
        Assert.Equal(2, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(new[] { 1, 2 }, deliveries);
    }

    [Fact]
    public void PacketDeliveryMethods_AdvanceIndependently()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(100),
        };
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<string>();

        scheduler.Schedule(sender, receiver, "packet:Unreliable", () => deliveries.Add("unreliable"));
        scheduler.SetLatency(sender, receiver, TimeSpan.FromMilliseconds(5));
        scheduler.Schedule(sender, receiver, "packet:ReliableOrdered", () => deliveries.Add("reliable"));

        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5)));
        Assert.Equal(new[] { "reliable" }, deliveries);
        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(95)));
        Assert.Equal(new[] { "reliable", "unreliable" }, deliveries);
    }

    [Fact]
    public void EqualDueTimes_DeliverInSchedulingSequence()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(10),
        };
        var firstSender = new object();
        var secondSender = new object();
        var firstReceiver = new object();
        var secondReceiver = new object();
        var deliveries = new List<int>();

        scheduler.Schedule(firstSender, firstReceiver, "message", () => deliveries.Add(1));
        scheduler.Schedule(secondSender, secondReceiver, "packet:Unreliable", () => deliveries.Add(2));
        scheduler.Schedule(firstSender, secondReceiver, "packet:ReliableOrdered", () => deliveries.Add(3));

        Assert.Equal(3, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10)));
        Assert.Equal(new[] { 1, 2, 3 }, deliveries);
    }

    [Fact]
    public void DrainReady_DeliversOnlyCurrentlyDueTrafficWithoutAdvancingTime()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(25),
        };
        var sender = new object();
        var readyReceiver = new object();
        var delayedReceiver = new object();
        var deliveries = new List<string>();

        scheduler.SetLatency(sender, readyReceiver, TimeSpan.Zero);
        scheduler.Schedule(sender, readyReceiver, "message", () => deliveries.Add("ready"));
        scheduler.Schedule(sender, delayedReceiver, "message", () => deliveries.Add("delayed"));

        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(new[] { "ready" }, deliveries);
        Assert.Equal(TimeSpan.Zero, scheduler.CurrentTime);
        Assert.Equal(1, scheduler.PendingDeliveryCount);
    }

    [Fact]
    public void Cancel_RemovesReceiverTrafficAndResetsItsStream()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(50),
        };
        var sender = new object();
        var receiver = new object();
        var otherReceiver = new object();
        var deliveries = new List<string>();

        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("cancelled"));
        scheduler.Schedule(sender, otherReceiver, "message", () => deliveries.Add("other"));

        Assert.Equal(1, scheduler.Cancel(receiver));
        Assert.Equal(1, scheduler.PendingDeliveryCount);

        scheduler.SetLatency(sender, receiver, TimeSpan.Zero);
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("replacement"));

        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(new[] { "replacement" }, deliveries);
        Assert.Equal(1, scheduler.PendingDeliveryCount);
    }

    [Fact]
    public void LinkLatencyOverrideAndClear_UsesDefaultAfterClear()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(20),
        };
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<string>();

        scheduler.SetLatency(sender, receiver, TimeSpan.FromMilliseconds(5));
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("override"));
        scheduler.ClearLatency(sender, receiver);
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("default"));

        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5)));
        Assert.Equal(new[] { "override" }, deliveries);
        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(15)));
        Assert.Equal(new[] { "override", "default" }, deliveries);
    }

    [Fact]
    public void ChannelsAndLinks_AdvanceIndependently()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(100),
        };
        var sender = new object();
        var firstReceiver = new object();
        var secondReceiver = new object();
        var deliveries = new List<string>();

        scheduler.Schedule(sender, firstReceiver, "message", () => deliveries.Add("slow-message"));
        scheduler.SetLatency(sender, firstReceiver, TimeSpan.FromMilliseconds(5));
        scheduler.Schedule(sender, firstReceiver, "packet:Unreliable", () => deliveries.Add("fast-packet"));
        scheduler.SetLatency(sender, secondReceiver, TimeSpan.FromMilliseconds(10));
        scheduler.Schedule(sender, secondReceiver, "message", () => deliveries.Add("other-link"));

        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5)));
        Assert.Equal(new[] { "fast-packet" }, deliveries);
        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5)));
        Assert.Equal(new[] { "fast-packet", "other-link" }, deliveries);

        Assert.Equal(1, scheduler.RunUntilIdle());
        Assert.Equal(new[] { "fast-packet", "other-link", "slow-message" }, deliveries);
        Assert.Equal(TimeSpan.FromMilliseconds(100), scheduler.CurrentTime);
    }
}
