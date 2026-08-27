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

    [Fact]
    public void ReducedLatency_DoesNotReorderOneDirectedChannel()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(100),
        };
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<int>();

        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add(1));
        scheduler.DefaultLatency = TimeSpan.Zero;
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add(2));

        Assert.Empty(deliveries);
        Assert.Equal(2, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(new[] { 1, 2 }, deliveries);
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
