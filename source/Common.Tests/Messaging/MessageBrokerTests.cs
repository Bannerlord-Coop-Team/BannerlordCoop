using Common.Messaging;
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
    /// Broker that exposes its own subscription table, because the entry and key counts are what these tests assert.
    /// </summary>
    private sealed class ProbeBroker : MessageBroker
    {
        public bool HasEntriesFor<T>() => subscribers.ContainsKey(typeof(T));

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
    public void Unsubscribe_WhenOnlyDeadEntriesRemain_RemovesTheMessageTypeKey()
    {
        var broker = new ProbeBroker();
        var collected = SubscribeCollectableSubscriber(broker);
        var live = new Subscriber();
        broker.Subscribe<ProbeMessage>(live.Handle);
        Collect();
        Assert.False(collected.IsAlive);

        broker.Unsubscribe<ProbeMessage>(live.Handle);

        Assert.False(broker.HasEntriesFor<ProbeMessage>());
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

    // The lambda's closure is the subscription target, and nothing roots it once this frame returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SubscribeUnrootedLambda(MessageBroker broker, List<string> received)
    {
        broker.Subscribe<ProbeMessage>(payload => received.Add("lambda"));
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
