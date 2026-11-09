using Common.Logging;
using Common.Messaging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Common.Tests.Messaging;

/// <summary>
/// Covers how the broker keeps, invokes and drops its weak subscriptions.
/// </summary>
public class MessageBrokerTests
{
    private static int targetlessReceived;

    /// <summary>
    /// Message type used only by these tests.
    /// </summary>
    private sealed class ProbeMessage : IMessage
    {
    }

    /// <summary>
    /// Second message type, so a test can publish one type while another type is subscribed to.
    /// </summary>
    private sealed class OtherProbeMessage : IMessage
    {
    }

    /// <summary>
    /// Subscriber whose instance method records what it received.
    /// </summary>
    private sealed class Subscriber
    {
        private readonly string name;
        private readonly List<string>? log;

        public Subscriber() : this("subscriber", null)
        {
        }

        public Subscriber(string name, List<string>? log)
        {
            this.name = name;
            this.log = log;
        }

        public int Received { get; private set; }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            Received++;
            log?.Add(name);
        }

        public void HandleOther(MessagePayload<OtherProbeMessage> payload)
        {
            Received++;
            log?.Add(name);
        }
    }

    /// <summary>
    /// Subscriber whose handler always throws, to exercise the failure logging of a publish.
    /// </summary>
    private sealed class ThrowingSubscriber
    {
        public int Received { get; private set; }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            Received++;
            throw new InvalidOperationException("probe handler failure");
        }
    }

    /// <summary>
    /// Subscriber that adds another subscriber for the same message type while that type is being published.
    /// </summary>
    private sealed class ReentrantSubscriber
    {
        private readonly MessageBroker broker;
        private readonly Subscriber joiner;
        private readonly List<string> log;

        public ReentrantSubscriber(MessageBroker broker, Subscriber joiner, List<string> log)
        {
            this.broker = broker;
            this.joiner = joiner;
            this.log = log;
        }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            log.Add("reentrant");
            broker.Subscribe<ProbeMessage>(joiner.Handle);
        }
    }

    /// <summary>
    /// Subscriber that lets an earlier subscriber die and then subscribes another one, all from inside a publish.
    /// </summary>
    private sealed class PruneTriggeringSubscriber
    {
        private readonly MessageBroker broker;
        private readonly Subscriber?[] earlier;
        private readonly Subscriber joiner;
        private readonly List<string> log;

        public PruneTriggeringSubscriber(MessageBroker broker, Subscriber?[] earlier, Subscriber joiner, List<string> log)
        {
            this.broker = broker;
            this.earlier = earlier;
            this.joiner = joiner;
            this.log = log;
        }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            log.Add("trigger");
            earlier[0] = null;
            Collect();
            broker.Subscribe<ProbeMessage>(joiner.Handle);
        }
    }

    /// <summary>
    /// Subscriber that subscribes a listener for a second message type while the first type is being published.
    /// </summary>
    private sealed class ForeignTypeSubscriber
    {
        private readonly MessageBroker broker;
        private readonly Subscriber joiner;

        public ForeignTypeSubscriber(MessageBroker broker, Subscriber joiner)
        {
            this.broker = broker;
            this.joiner = joiner;
        }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            broker.Subscribe<OtherProbeMessage>(joiner.HandleOther);
        }
    }

    /// <summary>
    /// Subscriber that unsubscribes itself from inside its own callback, the way a state drops its
    /// subscriptions while handling the message that ends it.
    /// </summary>
    private sealed class SelfUnsubscribingSubscriber
    {
        private readonly MessageBroker broker;
        private readonly List<string> log;

        public SelfUnsubscribingSubscriber(MessageBroker broker, List<string> log)
        {
            this.broker = broker;
            this.log = log;
        }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            log.Add("teardown");
            broker.Unsubscribe<ProbeMessage>(Handle);
        }
    }

    /// <summary>
    /// Subscriber that unsubscribes another subscriber from inside its callback, the way a handler disposes
    /// an object that also listens for the message being handled.
    /// </summary>
    private sealed class PeerUnsubscribingSubscriber
    {
        private readonly MessageBroker broker;
        private readonly Subscriber peer;
        private readonly List<string> log;

        public PeerUnsubscribingSubscriber(MessageBroker broker, Subscriber peer, List<string> log)
        {
            this.broker = broker;
            this.peer = peer;
            this.log = log;
        }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            log.Add("disposer");
            broker.Unsubscribe<ProbeMessage>(peer.Handle);
        }
    }

    /// <summary>
    /// Subscriber that unsubscribes itself and subscribes its successor from inside its callback, the way a state
    /// hands a message type over to the state that replaces it.
    /// </summary>
    private sealed class HandingOverSubscriber
    {
        private readonly MessageBroker broker;
        private readonly Subscriber successor;
        private readonly List<string> log;

        public HandingOverSubscriber(MessageBroker broker, Subscriber successor, List<string> log)
        {
            this.broker = broker;
            this.successor = successor;
            this.log = log;
        }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            log.Add("handover");
            broker.Unsubscribe<ProbeMessage>(Handle);
            broker.Subscribe<ProbeMessage>(successor.Handle);
        }
    }

    /// <summary>
    /// Subscriber that holds a publish inside its callback until another thread has changed the subscriptions,
    /// so the change lands while the walk is part way through the list.
    /// </summary>
    private sealed class GateSubscriber
    {
        private readonly ManualResetEventSlim reached;
        private readonly ManualResetEventSlim release;

        public GateSubscriber(ManualResetEventSlim reached, ManualResetEventSlim release)
        {
            this.reached = reached;
            this.release = release;
        }

        public bool TimedOut { get; private set; }

        public void Handle(MessagePayload<ProbeMessage> payload)
        {
            reached.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                TimedOut = true;
        }
    }

    /// <summary>
    /// Broker that exposes its own subscription table, because the entry counts are what these tests assert.
    /// </summary>
    private sealed class ProbeBroker : MessageBroker
    {
        public int EntryCountFor<T>() => subscribers.ContainsKey(typeof(T)) ? subscribers[typeof(T)].Count : 0;
    }

    [Fact]
    public void Subscribe_WhenAnEarlierTargetWasCollected_RemovesTheDeadEntry()
    {
        var broker = new ProbeBroker();
        var collected = SubscribeCollectableSubscriber(broker);
        Collect();
        Assert.False(collected.IsAlive);

        var live = new Subscriber();
        broker.Subscribe<ProbeMessage>(live.Handle);

        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(live);
    }

    [Fact]
    public void Unsubscribe_WhenOnlyDeadEntriesRemain_LeavesNoEntries()
    {
        var broker = new ProbeBroker();
        var collected = SubscribeCollectableSubscriber(broker);
        var live = new Subscriber();
        broker.Subscribe<ProbeMessage>(live.Handle);
        Collect();
        Assert.False(collected.IsAlive);

        broker.Unsubscribe<ProbeMessage>(live.Handle);

        Assert.Equal(0, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(live);
    }

    [Fact]
    public void Subscribe_WithRepeatedShortLivedSubscribers_DoesNotGrowTheEntryCount()
    {
        var broker = new ProbeBroker();
        WeakReference? last = null;

        for (int round = 0; round < 10; round++)
        {
            last = SubscribeCollectableSubscriber(broker);
            Collect();
        }

        Assert.NotNull(last);
        Assert.False(last!.IsAlive);
        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
    }

    [Fact]
    public void Subscribe_WhenAnEarlierTargetWasCollected_KeepsLiveSubscribersReceiving()
    {
        var broker = new ProbeBroker();
        var first = new Subscriber();
        broker.Subscribe<ProbeMessage>(first.Handle);
        var collected = SubscribeCollectableSubscriber(broker);
        Collect();
        Assert.False(collected.IsAlive);

        var second = new Subscriber();
        broker.Subscribe<ProbeMessage>(second.Handle);
        broker.Publish(this, new ProbeMessage());

        Assert.Equal(1, first.Received);
        Assert.Equal(1, second.Received);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void Publish_WithADeadEntryBetweenLiveSubscribers_InvokesBothInRegistrationOrder()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var first = new Subscriber("first", order);
        broker.Subscribe<ProbeMessage>(first.Handle);
        var collected = SubscribeCollectableSubscriber(broker);
        var last = new Subscriber("last", order);
        broker.Subscribe<ProbeMessage>(last.Handle);
        Collect();
        Assert.False(collected.IsAlive);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "first", "last" }, order);
        GC.KeepAlive(first);
        GC.KeepAlive(last);
    }

    [Fact]
    public void Publish_WhenAHandlerSubscribesForTheSameType_DoesNotSkipTheNextLiveSubscriber()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var joiner = new Subscriber("joiner", order);
        var reentrant = new ReentrantSubscriber(broker, joiner, order);
        broker.Subscribe<ProbeMessage>(reentrant.Handle);
        var collected = SubscribeCollectableSubscriber(broker);
        var last = new Subscriber("last", order);
        broker.Subscribe<ProbeMessage>(last.Handle);
        Collect();
        Assert.False(collected.IsAlive);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "reentrant", "last", "joiner" }, order);
        GC.KeepAlive(joiner);
        GC.KeepAlive(reentrant);
        GC.KeepAlive(last);
    }

    [Fact]
    public void Publish_WhenPruningIsTriggeredMidCallback_DoesNotSkipALiveSubscriber()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var earlier = new Subscriber?[1];
        var joiner = new Subscriber("joiner", order);
        var trigger = new PruneTriggeringSubscriber(broker, earlier, joiner, order);
        var middle = new Subscriber("middle", order);
        var last = new Subscriber("last", order);
        SubscribeIntoSlot(broker, earlier, "earlier", order);
        broker.Subscribe<ProbeMessage>(trigger.Handle);
        broker.Subscribe<ProbeMessage>(middle.Handle);
        broker.Subscribe<ProbeMessage>(last.Handle);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "earlier", "trigger", "middle", "last", "joiner" }, order);
        GC.KeepAlive(joiner);
        GC.KeepAlive(trigger);
        GC.KeepAlive(middle);
        GC.KeepAlive(last);
    }

    [Fact]
    public void Subscribe_ForAnotherTypeWhileAPublishRuns_RemovesThatTypesDeadEntry()
    {
        var broker = new ProbeBroker();
        var collected = SubscribeCollectableOtherSubscriber(broker);
        Collect();
        Assert.False(collected.IsAlive);
        var joiner = new Subscriber();
        var driver = new ForeignTypeSubscriber(broker, joiner);
        broker.Subscribe<ProbeMessage>(driver.Handle);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(1, broker.EntryCountFor<OtherProbeMessage>());
        GC.KeepAlive(joiner);
        GC.KeepAlive(driver);
    }

    [Fact]
    public void Unsubscribe_FromInsideAPublishOfThatType_RemovesTheDeadEntryOnceThePublishEnds()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var live = new Subscriber("live", order);
        broker.Subscribe<ProbeMessage>(live.Handle);
        var teardown = new SelfUnsubscribingSubscriber(broker, order);
        broker.Subscribe<ProbeMessage>(teardown.Handle);
        var collected = SubscribeCollectableSubscriber(broker);
        Collect();
        Assert.False(collected.IsAlive);
        Assert.Equal(3, broker.EntryCountFor<ProbeMessage>());

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "live", "teardown" }, order);
        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(live);
        GC.KeepAlive(teardown);
    }

    [Fact]
    public void Unsubscribe_FromInsideAPublishThatLeavesOnlyDeadEntries_LeavesNoEntries()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var teardown = new SelfUnsubscribingSubscriber(broker, order);
        broker.Subscribe<ProbeMessage>(teardown.Handle);
        var collected = SubscribeCollectableSubscriber(broker);
        Collect();
        Assert.False(collected.IsAlive);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(0, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(teardown);
    }

    [Fact]
    public void Unsubscribe_RemovesOnlyTheGivenTargetAndMethod()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var first = new Subscriber("first", order);
        var second = new Subscriber("second", order);
        broker.Subscribe<ProbeMessage>(first.Handle);
        broker.Subscribe<ProbeMessage>(second.Handle);

        broker.Unsubscribe<ProbeMessage>(first.Handle);
        broker.Publish(this, new ProbeMessage());

        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        Assert.Equal(new[] { "second" }, order);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void Subscribe_WithTheSameTargetAndMethodTwice_KeepsOneEntry()
    {
        var broker = new ProbeBroker();
        var live = new Subscriber();

        broker.Subscribe<ProbeMessage>(live.Handle);
        broker.Subscribe<ProbeMessage>(live.Handle);

        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(live);
    }

    [Fact]
    public void Publish_WithAnUnrootedLambdaSubscription_DoesNotReachItAfterCollection()
    {
        var broker = new ProbeBroker();
        var received = new List<string>();
        SubscribeUnrootedLambda(broker, received);
        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        Collect();

        broker.Publish(this, new ProbeMessage());

        Assert.Empty(received);
        Assert.Equal(0, broker.EntryCountFor<ProbeMessage>());
    }

    [Fact]
    public void Subscribe_WithATargetlessDelegate_TreatsItAsDeadAndNeverInvokesIt()
    {
        var broker = new ProbeBroker();
        targetlessReceived = 0;
        broker.Subscribe<ProbeMessage>(HandleTargetless);
        var live = new Subscriber();

        broker.Subscribe<ProbeMessage>(live.Handle);

        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        broker.Publish(this, new ProbeMessage());
        Assert.Equal(0, targetlessReceived);
        Assert.Equal(1, live.Received);
        GC.KeepAlive(live);
    }

    [Fact]
    public void Publish_WhenAHandlerThrows_LogsTheHandlerMethodName()
    {
        var broker = new ProbeBroker();
        var thrower = new ThrowingSubscriber();
        broker.Subscribe<ProbeMessage>(thrower.Handle);

        var messages = PublishAndCaptureLog(broker);

        Assert.Contains(messages, message => message.Contains("Failed to run \"Handle\" for \"ProbeMessage\""));
        GC.KeepAlive(thrower);
    }

    [Fact]
    public void Publish_WhenAHandlerThrows_LogsTheHandlerExceptionInsteadOfTheReflectionWrapper()
    {
        var broker = new ProbeBroker();
        var thrower = new ThrowingSubscriber();
        broker.Subscribe<ProbeMessage>(thrower.Handle);

        var messages = PublishAndCaptureLog(broker);

        var failure = Assert.Single(messages, message => message.Contains("Failed to run \"Handle\" for \"ProbeMessage\""));
        Assert.Contains("probe handler failure", failure);
        Assert.DoesNotContain(nameof(TargetInvocationException), failure);
        GC.KeepAlive(thrower);
    }

    [Fact]
    public void Publish_WhenAHandlerThrows_StillInvokesTheLaterSubscriber()
    {
        var broker = new ProbeBroker();
        var thrower = new ThrowingSubscriber();
        var later = new Subscriber();
        broker.Subscribe<ProbeMessage>(thrower.Handle);
        broker.Subscribe<ProbeMessage>(later.Handle);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(1, thrower.Received);
        Assert.Equal(1, later.Received);
        GC.KeepAlive(thrower);
        GC.KeepAlive(later);
    }

    [Fact]
    public void Publish_WhenAHandlerUnsubscribesItself_StillInvokesTheNextSubscriber()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var first = new Subscriber("first", order);
        var teardown = new SelfUnsubscribingSubscriber(broker, order);
        var last = new Subscriber("last", order);
        broker.Subscribe<ProbeMessage>(first.Handle);
        broker.Subscribe<ProbeMessage>(teardown.Handle);
        broker.Subscribe<ProbeMessage>(last.Handle);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "first", "teardown", "last" }, order);
        Assert.Equal(2, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(first);
        GC.KeepAlive(teardown);
        GC.KeepAlive(last);
    }

    [Fact]
    public void Publish_WhenAHandlerUnsubscribesALaterSubscriber_DoesNotInvokeIt()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var disposed = new Subscriber("disposed", order);
        var disposer = new PeerUnsubscribingSubscriber(broker, disposed, order);
        var last = new Subscriber("last", order);
        broker.Subscribe<ProbeMessage>(disposer.Handle);
        broker.Subscribe<ProbeMessage>(disposed.Handle);
        broker.Subscribe<ProbeMessage>(last.Handle);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "disposer", "last" }, order);
        GC.KeepAlive(disposed);
        GC.KeepAlive(disposer);
        GC.KeepAlive(last);
    }

    [Fact]
    public void Publish_WhenTheLastLiveSubscriberHandsOverToANewOne_InvokesTheNewOne()
    {
        var broker = new ProbeBroker();
        var order = new List<string>();
        var successor = new Subscriber("successor", order);
        var handover = new HandingOverSubscriber(broker, successor, order);
        broker.Subscribe<ProbeMessage>(handover.Handle);
        var collected = SubscribeCollectableSubscriber(broker);
        Collect();
        Assert.False(collected.IsAlive);

        broker.Publish(this, new ProbeMessage());

        Assert.Equal(new[] { "handover", "successor" }, order);
        Assert.Equal(1, broker.EntryCountFor<ProbeMessage>());
        GC.KeepAlive(successor);
        GC.KeepAlive(handover);
    }

    [Fact]
    public void Publish_WhileAnotherThreadUnsubscribesEarlierSubscribers_InvokesTheRemainingSubscriberOncePerPublish()
    {
        const int rounds = 300;
        const int leavingPerRound = 64;
        var failures = new List<string>();

        for (int round = 0; round < rounds && failures.Count == 0; round++)
        {
            var broker = new ProbeBroker();
            using var reached = new ManualResetEventSlim();
            using var removed = new ManualResetEventSlim();
            var gate = new GateSubscriber(reached, removed);
            var staying = new Subscriber();
            var leaving = new Subscriber[leavingPerRound];
            for (int i = 0; i < leaving.Length; i++)
                leaving[i] = new Subscriber();
            // Half the leaving subscribers come before the gate, so removing one shifts entries the walk already passed
            for (int i = 0; i < leaving.Length / 2; i++)
                broker.Subscribe<ProbeMessage>(leaving[i].Handle);
            broker.Subscribe<ProbeMessage>(gate.Handle);
            broker.Subscribe<ProbeMessage>(staying.Handle);
            for (int i = leaving.Length / 2; i < leaving.Length; i++)
                broker.Subscribe<ProbeMessage>(leaving[i].Handle);

            // The first removal lands while a publish is held at the gate, and the rest race the publishes that follow
            var unsubscriber = Task.Run(() =>
            {
                if (!reached.Wait(TimeSpan.FromSeconds(10)))
                    return false;
                broker.Unsubscribe<ProbeMessage>(leaving[0].Handle);
                removed.Set();
                foreach (var subscriber in leaving.Skip(1))
                    broker.Unsubscribe<ProbeMessage>(subscriber.Handle);
                return true;
            });

            int published = 0;
            try
            {
                do
                {
                    broker.Publish(this, new ProbeMessage());
                    published++;
                }
                while (!unsubscriber.IsCompleted);
            }
            catch (Exception ex)
            {
                failures.Add($"round {round}: publish threw {ex.GetType().Name}: {ex.Message}");
            }

            if (!unsubscriber.Result || gate.TimedOut)
                failures.Add($"round {round}: the publish and the first removal never met at the gate");
            else if (failures.Count == 0 && staying.Received != published)
                failures.Add($"round {round}: the staying subscriber ran {staying.Received} times for {published} publishes");
            GC.KeepAlive(gate);
            GC.KeepAlive(leaving);
            GC.KeepAlive(staying);
        }

        Assert.Empty(failures);
    }

    private static void HandleTargetless(MessagePayload<ProbeMessage> payload) => targetlessReceived++;

    // Keeps the new subscriber out of the calling frame so the array slot stays its only root.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SubscribeIntoSlot(MessageBroker broker, Subscriber?[] slot, string name, List<string> log)
    {
        var subscriber = new Subscriber(name, log);
        slot[0] = subscriber;
        broker.Subscribe<ProbeMessage>(subscriber.Handle);
    }

    // Subscribes from its own stack frame so nothing roots the target once it returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SubscribeCollectableSubscriber(MessageBroker broker)
    {
        var subscriber = new Subscriber();
        broker.Subscribe<ProbeMessage>(subscriber.Handle);
        return new WeakReference(subscriber);
    }

    // Subscribes for the second message type from its own stack frame, so nothing roots the target.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SubscribeCollectableOtherSubscriber(MessageBroker broker)
    {
        var subscriber = new Subscriber();
        broker.Subscribe<OtherProbeMessage>(subscriber.HandleOther);
        return new WeakReference(subscriber);
    }

    // The lambda's closure is the subscription target, and nothing roots it once this frame returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SubscribeUnrootedLambda(MessageBroker broker, List<string> received)
    {
        broker.Subscribe<ProbeMessage>(payload => received.Add("lambda"));
    }

    // Other test classes log through the same process-wide sink in parallel, so callers assert on the
    // line this publish produced and never on the capture as a whole.
    private ConcurrentQueue<string> PublishAndCaptureLog(MessageBroker broker)
    {
        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            broker.Publish(this, new ProbeMessage());
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(callback);
        }

        return messages;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
