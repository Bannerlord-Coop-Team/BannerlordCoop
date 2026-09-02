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
    [InlineData("packet:ReliableOrdered")]
    [InlineData("ReliableOrdered:channel-0")]
    public void ReducedLatency_DoesNotReorderReliableOrderedChannel(string channel)
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

    [Theory]
    [InlineData("ReliableUnordered:channel-0")]
    [InlineData("Unreliable:channel-0")]
    public void ReducedLatency_AllowsUnorderedTrafficToOvertake(string channel)
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

        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(new[] { 2 }, deliveries);
        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(new[] { 2, 1 }, deliveries);
    }

    [Theory]
    [InlineData("Sequenced:channel-0")]
    [InlineData("ReliableSequenced:channel-0")]
    public void NewSequencedTraffic_SupersedesOlderPendingTraffic(string channel)
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

        Assert.Equal(1, scheduler.PendingDeliveryCount);
        Assert.Contains(scheduler.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Superseded);
        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(new[] { 2 }, deliveries);
        Assert.Equal(0, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(new[] { 2 }, deliveries);
    }

    [Theory]
    [InlineData("Sequenced:channel-0")]
    [InlineData("ReliableSequenced:channel-0")]
    public void HigherLatencySequencedTraffic_DoesNotSupersedeEarlierArrival(string channel)
    {
        var scheduler = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<int>();

        scheduler.Schedule(sender, receiver, channel, () => deliveries.Add(1));
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(100);
        scheduler.Schedule(sender, receiver, channel, () => deliveries.Add(2));

        Assert.Equal(2, scheduler.PendingDeliveryCount);
        Assert.DoesNotContain(scheduler.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Superseded);
        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(new[] { 1 }, deliveries);
        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
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

    [Fact]
    public void StaleConnectionSnapshot_IsRejectedAfterReconnect()
    {
        var scheduler = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        VirtualNetworkConnection staleConnection = scheduler.CaptureConnection(sender, receiver);
        long originalReceiverGeneration = scheduler.GetConnectionGeneration(receiver);
        int deliveries = 0;

        Assert.Equal(0, scheduler.Disconnect(receiver));
        scheduler.Reconnect(receiver);
        Assert.Throws<VirtualNetworkStaleConnectionException>(() =>
            scheduler.Schedule(staleConnection, "message", () => deliveries++));

        Assert.Equal(originalReceiverGeneration + 2, scheduler.GetConnectionGeneration(receiver));
        Assert.Equal(0, scheduler.PendingDeliveryCount);
        Assert.Equal(0, scheduler.RunUntilIdle());
        Assert.Equal(0, deliveries);
        Assert.Contains(
            scheduler.Trace,
            entry => entry.Kind == VirtualNetworkTraceKind.StaleConnectionRejected);
    }

    [Fact]
    public void Disconnect_CancelsOldGenerationTraffic_AndReconnectAcceptsNewTraffic()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(20),
        };
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<string>();

        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("old"));

        Assert.Equal(1, scheduler.Disconnect(receiver));
        Assert.False(scheduler.IsConnected(receiver));
        Assert.Equal(0, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(20)));

        scheduler.Reconnect(receiver);
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("new"));

        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(20)));
        Assert.Equal(new[] { "new" }, deliveries);
        Assert.Contains(scheduler.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Canceled);
    }

    [Fact]
    public void PausedLink_HoldsReliableTraffic_WithoutBlockingOtherLinkOrChannel()
    {
        var scheduler = new VirtualNetworkScheduler();
        var sender = new object();
        var pausedReceiver = new object();
        var activeReceiver = new object();
        var deliveries = new List<string>();

        scheduler.PauseLink(sender, pausedReceiver);
        scheduler.Schedule(sender, pausedReceiver, "message", () => deliveries.Add("paused-message"));
        scheduler.Schedule(
            sender,
            pausedReceiver,
            "packet:ReliableOrdered",
            () => deliveries.Add("paused-packet"));
        scheduler.Schedule(
            sender,
            activeReceiver,
            "packet:Unreliable",
            () => deliveries.Add("active-packet"));

        Assert.Equal(1, scheduler.RunUntilIdle());
        Assert.Equal(new[] { "active-packet" }, deliveries);
        Assert.Equal(2, scheduler.PendingDeliveryCount);
        Assert.True(scheduler.IsLinkPaused(sender, pausedReceiver));

        scheduler.ResumeLink(sender, pausedReceiver);

        Assert.Equal(2, scheduler.DrainReady());
        Assert.Equal(new[] { "active-packet", "paused-message", "paused-packet" }, deliveries);
        Assert.Equal(0, scheduler.PendingDeliveryCount);
    }

    [Fact]
    public void PartitionDisconnectReconnect_DoesNotDeliverOldConnectionTraffic()
    {
        var scheduler = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<string>();

        scheduler.PauseLink(sender, receiver);
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("old"));
        Assert.Equal(1, scheduler.Disconnect(receiver));
        scheduler.Reconnect(receiver);
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add("new"));

        Assert.Equal(0, scheduler.RunUntilIdle());
        Assert.Empty(deliveries);
        Assert.Equal(1, scheduler.PendingDeliveryCount);

        scheduler.ResumeLink(sender, receiver);

        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(new[] { "new" }, deliveries);
    }

    [Fact]
    public void PendingLimit_ReportsBackpressureAndHighWaterMark_UntilTrafficDrains()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            PendingDeliveryLimit = 2,
        };
        var sender = new object();
        var receiver = new object();
        var deliveries = new List<int>();

        scheduler.PauseLink(sender, receiver);
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add(1));
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add(2));

        VirtualNetworkBackpressureException exception = Assert.Throws<VirtualNetworkBackpressureException>(() =>
            scheduler.Schedule(sender, receiver, "message", () => deliveries.Add(3)));

        Assert.Equal(2, exception.PendingDeliveryLimit);
        Assert.Equal(2, scheduler.PendingDeliveryCount);
        Assert.Equal(2, scheduler.PendingDeliveryHighWaterMark);
        Assert.Equal(1, scheduler.BackpressureCount);
        Assert.Equal(2, scheduler.GetPendingDeliveryCount(sender, receiver));
        Assert.Contains(scheduler.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Backpressure);

        scheduler.ResumeLink(sender, receiver);
        Assert.Equal(2, scheduler.DrainReady());
        scheduler.Schedule(sender, receiver, "message", () => deliveries.Add(3));
        Assert.Equal(1, scheduler.DrainReady());

        Assert.Equal(new[] { 1, 2, 3 }, deliveries);
        Assert.Equal(2, scheduler.PendingDeliveryHighWaterMark);
    }

    [Fact]
    public void RecordedSchedulingInputs_ReplayTheSameDeterministicTrace()
    {
        var original = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(10),
        };
        var firstSender = new object();
        var firstReceiver = new object();
        var secondReceiver = new object();
        var originalDeliveries = new List<long>();

        original.Schedule(firstSender, firstReceiver, "message", () => originalDeliveries.Add(0));
        Assert.Equal(0, original.AdvanceBy(TimeSpan.FromMilliseconds(3)));
        original.SetLatency(firstSender, secondReceiver, TimeSpan.FromMilliseconds(2));
        original.Schedule(firstSender, secondReceiver, "packet:Unreliable", () => originalDeliveries.Add(1));
        VirtualNetworkReplay recording = original.CaptureReplay();
        IReadOnlyList<VirtualNetworkScheduleInput> inputs = recording.SchedulingInputs;

        Assert.Equal(2, original.RunUntilIdle());
        VirtualNetworkTraceEntry[] originalTrace = original.Trace.ToArray();

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>();
        foreach (long endpointId in inputs
                     .SelectMany(input => new[] { input.SenderEndpointId, input.ReceiverEndpointId })
                     .Distinct())
        {
            replayEndpoints.Add(endpointId, new object());
        }

        var replayDeliveries = new List<long>();
        replay.Replay(recording, endpointId => replayEndpoints[endpointId], input =>
            replayDeliveries.Add(input.DeliverySequence));

        Assert.Equal(inputs, replay.RecordedSchedulingInputs);
        Assert.Equal(2, replay.RunUntilIdle());
        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Equal(originalTrace, replay.Trace);
    }

    [Theory]
    [InlineData("Sequenced:channel-0")]
    [InlineData("ReliableSequenced:channel-0")]
    public void Replay_PreservesDrainBoundariesBetweenSequencedSends(string channel)
    {
        var original = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        var originalDeliveries = new List<long>();

        original.Schedule(sender, receiver, channel, () => originalDeliveries.Add(0));
        Assert.Equal(1, original.DrainReady());
        original.Schedule(sender, receiver, channel, () => originalDeliveries.Add(1));
        VirtualNetworkReplay recording = original.CaptureReplay();

        Assert.Equal(2, recording.TimeOperations.Count);
        Assert.Equal(1, original.DrainReady());
        VirtualNetworkTraceEntry[] originalTrace = original.Trace.ToArray();

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        var replayDeliveries = new List<long>();

        replay.Replay(recording, endpointId => replayEndpoints[endpointId], input =>
            replayDeliveries.Add(input.DeliverySequence));

        Assert.Equal(new long[] { 0 }, replayDeliveries);
        Assert.Equal(recording.TimeOperations, replay.RecordedTimeOperations);
        Assert.Equal(recording.DeliveryOperations, replay.RecordedDeliveryOperations);
        Assert.Equal(1, replay.DrainReady());
        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Equal(original.RecordedTimeOperations, replay.RecordedTimeOperations);
        Assert.Equal(original.RecordedDeliveryOperations, replay.RecordedDeliveryOperations);
        Assert.Equal(originalTrace, replay.Trace);
    }

    [Fact]
    public void Replay_PreservesReadyDeliveriesScheduledByDeliveryCallbacks()
    {
        var original = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        var originalDeliveries = new List<long>();

        original.Schedule(sender, receiver, "message", () =>
        {
            originalDeliveries.Add(0);
            original.Schedule(sender, receiver, "message", () => originalDeliveries.Add(1));
        });
        Assert.Equal(2, original.DrainReady());
        VirtualNetworkReplay recording = original.CaptureReplay();
        VirtualNetworkTraceEntry[] originalTrace = original.Trace.ToArray();

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        var replayDeliveries = new List<long>();

        replay.Replay(recording, endpointId => replayEndpoints[endpointId], input =>
            replayDeliveries.Add(input.DeliverySequence));

        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Empty(replay.CaptureState().PendingDeliveries);
        Assert.Equal(recording.SchedulingInputs, replay.RecordedSchedulingInputs);
        Assert.Equal(recording.TimeOperations, replay.RecordedTimeOperations);
        Assert.Equal(recording.DeliveryOperations, replay.RecordedDeliveryOperations);
        Assert.Equal(originalTrace, replay.Trace);
    }

    [Fact]
    public void Replay_ContinuesAfterHandledDeliveryFailure()
    {
        var original = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        var originalDeliveries = new List<long>();

        original.Schedule(sender, receiver, "message", () =>
        {
            originalDeliveries.Add(0);
            throw new InvalidOperationException("expected failure");
        });
        Assert.Throws<InvalidOperationException>(() => original.DrainReady());
        original.Schedule(sender, receiver, "message", () => originalDeliveries.Add(1));
        Assert.Equal(1, original.DrainReady());
        VirtualNetworkReplay recording = original.CaptureReplay();
        VirtualNetworkTraceEntry[] originalTrace = original.Trace.ToArray();

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        var replayDeliveries = new List<long>();

        replay.Replay(recording, endpointId => replayEndpoints[endpointId], input =>
            replayDeliveries.Add(input.DeliverySequence));

        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Empty(replay.CaptureState().PendingDeliveries);
        Assert.Equal(recording.TimeOperations, replay.RecordedTimeOperations);
        Assert.Equal(recording.DeliveryOperations, replay.RecordedDeliveryOperations);
        Assert.Equal(originalTrace, replay.Trace);
    }

    [Fact]
    public void Replay_PreservesNestedHandledDeliveryFailure()
    {
        var original = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        var originalDeliveries = new List<long>();

        original.Schedule(sender, receiver, "message", () =>
        {
            originalDeliveries.Add(0);
            original.Schedule(sender, receiver, "message", () =>
            {
                originalDeliveries.Add(1);
                throw new InvalidOperationException("expected nested failure");
            });
            Assert.Throws<InvalidOperationException>(() => original.DrainReady());
        });
        Assert.Equal(1, original.DrainReady());
        VirtualNetworkReplay recording = original.CaptureReplay();
        VirtualNetworkTraceEntry[] originalTrace = original.Trace.ToArray();

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        var replayDeliveries = new List<long>();

        replay.Replay(recording, endpointId => replayEndpoints[endpointId], input =>
            replayDeliveries.Add(input.DeliverySequence));

        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Empty(replay.CaptureState().PendingDeliveries);
        Assert.Equal(recording.TimeOperations, replay.RecordedTimeOperations);
        Assert.Equal(recording.DeliveryOperations, replay.RecordedDeliveryOperations);
        Assert.Equal(originalTrace, replay.Trace);
    }

    [Fact]
    public void Replay_RejectsDeliveryOutsideTimeOperation()
    {
        var recording = new VirtualNetworkReplay(
            new[]
            {
                new VirtualNetworkScheduleInput(
                    0,
                    0,
                    0,
                    1,
                    2,
                    "message",
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    0,
                    0),
            },
            Array.Empty<VirtualNetworkConnectionTransition>(),
            Array.Empty<VirtualNetworkLinkTransition>(),
            Array.Empty<VirtualNetworkTimeOperation>(),
            new[]
            {
                new VirtualNetworkDeliveryOperation(
                    1,
                    TimeSpan.Zero,
                    VirtualNetworkDeliveryOperationKind.Started,
                    0),
                new VirtualNetworkDeliveryOperation(
                    2,
                    TimeSpan.Zero,
                    VirtualNetworkDeliveryOperationKind.Completed,
                    0),
            });
        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };

        Assert.Throws<ArgumentException>(() => replay.Replay(
            recording,
            endpointId => replayEndpoints[endpointId],
            _ => { }));
    }

    [Fact]
    public void Replay_RejectsDrainReadyDeliveryAtFutureTime()
    {
        TimeSpan futureTime = TimeSpan.FromMilliseconds(1);
        var recording = new VirtualNetworkReplay(
            new[]
            {
                new VirtualNetworkScheduleInput(
                    0,
                    0,
                    0,
                    1,
                    2,
                    "message",
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    0,
                    0),
            },
            Array.Empty<VirtualNetworkConnectionTransition>(),
            Array.Empty<VirtualNetworkLinkTransition>(),
            new[]
            {
                new VirtualNetworkTimeOperation(
                    1,
                    TimeSpan.Zero,
                    VirtualNetworkTimeOperationKind.DrainReady,
                    VirtualNetworkTimeOperationPhase.Started,
                    TimeSpan.Zero),
                new VirtualNetworkTimeOperation(
                    4,
                    futureTime,
                    VirtualNetworkTimeOperationKind.DrainReady,
                    VirtualNetworkTimeOperationPhase.Completed,
                    TimeSpan.Zero),
            },
            new[]
            {
                new VirtualNetworkDeliveryOperation(
                    2,
                    futureTime,
                    VirtualNetworkDeliveryOperationKind.Started,
                    0),
                new VirtualNetworkDeliveryOperation(
                    3,
                    futureTime,
                    VirtualNetworkDeliveryOperationKind.Completed,
                    0),
            });
        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };

        Assert.Throws<ArgumentException>(() => replay.Replay(
            recording,
            endpointId => replayEndpoints[endpointId],
            _ => { }));
    }

    [Fact]
    public void Replay_BindsRecordedEndpointIds_WhenOtherEndpointsWereRegisteredFirst()
    {
        var original = new VirtualNetworkScheduler();
        var decoySender = new object();
        var decoyReceiver = new object();
        var sender = new object();
        var receiver = new object();
        int originalDeliveries = 0;

        original.SetLatency(decoySender, decoyReceiver, TimeSpan.FromMilliseconds(50));
        original.Schedule(sender, receiver, "message", () => originalDeliveries++);
        VirtualNetworkReplay recording = original.CaptureReplay();
        IReadOnlyList<VirtualNetworkScheduleInput> inputs = recording.SchedulingInputs;
        Assert.Equal(3, Assert.Single(inputs).SenderEndpointId);
        Assert.Equal(4, Assert.Single(inputs).ReceiverEndpointId);
        Assert.Equal(1, original.RunUntilIdle());

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [3] = new object(),
            [4] = new object(),
        };
        int replayDeliveries = 0;

        replay.Replay(recording, endpointId => replayEndpoints[endpointId], _ => replayDeliveries++);

        Assert.Equal(1, replay.RunUntilIdle());
        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Equal(inputs, replay.RecordedSchedulingInputs);
        Assert.Equal(original.Trace, replay.Trace);
    }

    [Fact]
    public void Replay_ReproducesDisconnectCancellationAndReconnectGeneration()
    {
        var original = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(10),
        };
        var sender = new object();
        var receiver = new object();
        var originalDeliveries = new List<long>();

        original.Schedule(sender, receiver, "message", () => originalDeliveries.Add(0));
        Assert.Equal(1, original.Disconnect(receiver));
        original.Reconnect(receiver);
        original.Schedule(sender, receiver, "message", () => originalDeliveries.Add(1));
        VirtualNetworkReplay recording = original.CaptureReplay();

        Assert.Equal(1, original.RunUntilIdle());
        Assert.Equal(new long[] { 1 }, originalDeliveries);

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        var replayDeliveries = new List<long>();

        replay.Replay(recording, endpointId => replayEndpoints[endpointId], input =>
            replayDeliveries.Add(input.DeliverySequence));

        Assert.Equal(recording.SchedulingInputs, replay.RecordedSchedulingInputs);
        Assert.Equal(recording.ConnectionTransitions, replay.RecordedConnectionTransitions);
        Assert.Equal(1, replay.RunUntilIdle());
        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Equal(original.Trace, replay.Trace);
        Assert.Equal(2, replay.GetConnectionGeneration(replayEndpoints[2]));

    }

    [Fact]
    public void Replay_ReproducesPartitionStateAcrossReconnect()
    {
        var original = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        int originalDeliveries = 0;

        original.PauseLink(sender, receiver);
        original.Disconnect(receiver);
        original.Reconnect(receiver);
        original.Schedule(sender, receiver, "message", () => originalDeliveries++);
        VirtualNetworkReplay recording = original.CaptureReplay();

        Assert.Equal(0, original.RunUntilIdle());
        Assert.True(original.IsLinkPaused(sender, receiver));

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        int replayDeliveries = 0;

        replay.Replay(
            recording,
            endpointId => replayEndpoints[endpointId],
            _ => replayDeliveries++);

        Assert.Equal(recording.LinkTransitions, replay.RecordedLinkTransitions);
        Assert.True(replay.IsLinkPaused(replayEndpoints[1], replayEndpoints[2]));
        Assert.Equal(0, replay.RunUntilIdle());
        Assert.Equal(0, replayDeliveries);

        original.ResumeLink(sender, receiver);
        replay.ResumeLink(replayEndpoints[1], replayEndpoints[2]);

        Assert.Equal(1, original.DrainReady());
        Assert.Equal(1, replay.DrainReady());
        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Equal(original.RecordedLinkTransitions, replay.RecordedLinkTransitions);
        Assert.Equal(original.Trace, replay.Trace);
    }

    [Fact]
    public void Replay_ReproducesPartitionAddedAfterSchedule()
    {
        var original = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        int originalDeliveries = 0;

        original.Schedule(sender, receiver, "message", () => originalDeliveries++);
        original.PauseLink(sender, receiver);
        VirtualNetworkReplay recording = original.CaptureReplay();

        Assert.Equal(0, original.RunUntilIdle());

        var replay = new VirtualNetworkScheduler();
        var replayEndpoints = new Dictionary<long, object>
        {
            [1] = new object(),
            [2] = new object(),
        };
        int replayDeliveries = 0;

        replay.Replay(
            recording,
            endpointId => replayEndpoints[endpointId],
            _ => replayDeliveries++);

        Assert.True(replay.IsLinkPaused(replayEndpoints[1], replayEndpoints[2]));
        Assert.Equal(0, replay.RunUntilIdle());
        Assert.Equal(0, replayDeliveries);

        original.ResumeLink(sender, receiver);
        replay.ResumeLink(replayEndpoints[1], replayEndpoints[2]);

        Assert.Equal(1, original.DrainReady());
        Assert.Equal(1, replay.DrainReady());
        Assert.Equal(originalDeliveries, replayDeliveries);
        Assert.Equal(original.Trace, replay.Trace);
    }

    [Fact]
    public void CaptureState_ExplainsAndHashesSchedulerState()
    {
        var scheduler = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(7),
            PendingDeliveryLimit = 2,
        };
        var sender = new object();
        var receiver = new object();
        var disconnected = new object();

        scheduler.SetLatency(sender, receiver, TimeSpan.FromMilliseconds(3));
        scheduler.PauseLink(sender, receiver);
        scheduler.Schedule(sender, receiver, "message", () => { });
        scheduler.Schedule(sender, receiver, "message", () => { });
        Assert.Throws<VirtualNetworkBackpressureException>(() =>
            scheduler.Schedule(sender, receiver, "message", () => { }));
        scheduler.Disconnect(disconnected);

        VirtualNetworkStateSnapshot state = scheduler.CaptureState();

        Assert.Equal(VirtualNetworkStateSnapshot.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Equal(0, state.CurrentTimeTicks);
        Assert.Equal(TimeSpan.FromMilliseconds(7).Ticks, state.DefaultLatencyTicks);
        Assert.Equal(2, state.PendingDeliveryLimit);
        Assert.Equal(2, state.PendingDeliveryHighWaterMark);
        Assert.Equal(1, state.BackpressureCount);
        Assert.Equal(
            new[]
            {
                new VirtualNetworkEndpointState(1, 0, true),
                new VirtualNetworkEndpointState(2, 0, true),
                new VirtualNetworkEndpointState(3, 1, false),
            },
            state.Endpoints);
        Assert.Equal(
            new[] { new VirtualNetworkLinkLatencyState(1, 2, TimeSpan.FromMilliseconds(3).Ticks) },
            state.LinkLatencies);
        Assert.Equal(
            new[] { new VirtualNetworkPausedLinkState(1, 2) },
            state.PausedLinks);
        Assert.Equal(2, state.PendingDeliveries.Count);
        Assert.All(state.PendingDeliveries, delivery =>
        {
            Assert.Equal(1, delivery.SenderEndpointId);
            Assert.Equal(2, delivery.ReceiverEndpointId);
            Assert.Equal("message", delivery.Channel);
            Assert.Equal(TimeSpan.FromMilliseconds(3).Ticks, delivery.DueTimeTicks);
        });
        Assert.Matches("^[0-9a-f]{64}$", state.StateDigest);

        var equivalent = new VirtualNetworkScheduler
        {
            DefaultLatency = TimeSpan.FromMilliseconds(7),
            PendingDeliveryLimit = 2,
        };
        var equivalentSender = new object();
        var equivalentReceiver = new object();
        var equivalentDisconnected = new object();
        equivalent.SetLatency(equivalentSender, equivalentReceiver, TimeSpan.FromMilliseconds(3));
        equivalent.PauseLink(equivalentSender, equivalentReceiver);
        equivalent.Schedule(equivalentSender, equivalentReceiver, "message", () => { });
        equivalent.Schedule(equivalentSender, equivalentReceiver, "message", () => { });
        Assert.Throws<VirtualNetworkBackpressureException>(() =>
            equivalent.Schedule(equivalentSender, equivalentReceiver, "message", () => { }));
        equivalent.Disconnect(equivalentDisconnected);

        Assert.Equal(state.StateDigest, equivalent.CaptureState().StateDigest);

        scheduler.ResumeLink(sender, receiver);
        Assert.NotEqual(state.StateDigest, scheduler.CaptureState().StateDigest);
    }

    [Fact]
    public void FailedDelivery_DoesNotStrandLaterReadyTraffic()
    {
        var scheduler = new VirtualNetworkScheduler();
        var sender = new object();
        var receiver = new object();
        int deliveries = 0;

        scheduler.Schedule(sender, receiver, "message", () => throw new InvalidOperationException("test"));
        scheduler.Schedule(sender, receiver, "message", () => deliveries++);

        Assert.Throws<InvalidOperationException>(() => scheduler.DrainReady());
        Assert.Equal(1, scheduler.PendingDeliveryCount);
        Assert.Equal(1, scheduler.DrainReady());
        Assert.Equal(1, deliveries);
        Assert.Contains(scheduler.Trace, entry => entry.Kind == VirtualNetworkTraceKind.DeliveryFailed);
        Assert.Contains(scheduler.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Delivered);
    }
}
