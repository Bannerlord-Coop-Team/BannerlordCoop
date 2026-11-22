using Common.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Common.Messaging;

public interface IMessageBroker : IDisposable
{
    void Publish<T>(object source, T message) where T : IMessage;

    void Subscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage;

    void Unsubscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage;
}

public class MessageBroker : IMessageBroker
{
    private static readonly ILogger Logger = LogManager.GetLogger<MessageBroker>();
    protected static MessageBroker instance;
    // Network handlers publish on the poller thread while the game thread subscribes and unsubscribes, so
    // every change goes through subscriptionLock and swaps in a new array instead of editing one a publish may be walking
    protected readonly ConcurrentDictionary<Type, SubscriberList> subscribers;
    private readonly object subscriptionLock = new object();
    // Sequence of the newest subscription. Arrays stay ordered by it, so a publish can find its place in a swapped array
    private long lastSequence;
    public static MessageBroker Instance { 
        get
        {
            instance ??= new MessageBroker();
            return instance;
        } 
    }

    public MessageBroker()
    {
        subscribers = new ConcurrentDictionary<Type, SubscriberList>();
    }

    public virtual void Publish<T>(object source, T message) where T : IMessage
    {
        if (message == null)
            return;

        if (!subscribers.TryGetValue(typeof(T), out var list)) return;

        var items = list.Items;
        if (items.Length == 0) return;
        var payload = new MessagePayload<T>(source, message);
        long passed = 0;
        int i = 0;
        while (true)
        {
            // A handler or another thread swapped the array, so carry on in the new one after the last sequence
            // passed, which reaches subscribers added during this publish and skips removed ones without repeating any
            var latest = list.Items;
            if (latest != items)
            {
                items = latest;
                i = 0;
                while (i < items.Length && items[i].Sequence <= passed) i++;
            }
            if (i >= items.Length) break;

            var subscription = items[i++];
            passed = subscription.Sequence;
            var weakDelegate = subscription.Handler;
            if (weakDelegate.IsAlive == false)
            {
                // Subscriptions are weak by design, but a collected target means this message is
                // silently not handled — name it, because a lost handler is otherwise invisible
                // (the classic trap: a closure/lambda subscription nothing kept alive).
                Logger.Warning("Dropping dead subscriber {Method} for {MessageType}: its target was garbage collected",
                    weakDelegate.Method?.Name ?? "<unknown>", typeof(T).Name);
                lock (subscriptionLock)
                {
                    list.Items = list.Items.Where(entry => entry.Handler.IsAlive).ToArray();
                }
                continue;
            }

            try
            {
                // Making synchronous to maintain sequencing of packets
                weakDelegate.Invoke(new object[] { payload });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // MethodInfo.Invoke wraps what the handler threw, and the wrapper's own frames end here
                Logger.Error(ex.InnerException, "Failed to run {Method} for {MessageType}",
                    weakDelegate.Method?.Name ?? "<unknown>", typeof(T).Name);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to run {Method} for {MessageType}",
                    weakDelegate.Method?.Name ?? "<unknown>", typeof(T).Name);
            }
        }
    }

    public virtual void Subscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage
    {
        var handler = new WeakDelegate(subscription);
        lock (subscriptionLock)
        {
            if (!subscribers.TryGetValue(typeof(T), out var list))
            {
                list = new SubscriberList();
                subscribers[typeof(T)] = list;
            }

            // Entries whose target was collected otherwise sit here until this message type is published again
            var kept = list.Items.Where(entry => entry.Handler.IsAlive).ToList();
            if (!kept.Any(entry => entry.Handler.Equals(handler)))
                kept.Add(new Subscription(handler, ++lastSequence));
            list.Items = kept.ToArray();
        }
    }

    public virtual void Unsubscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage
    {
        var handler = new WeakDelegate(subscription);
        lock (subscriptionLock)
        {
            if (!subscribers.TryGetValue(typeof(T), out var list)) return;

            // An emptied list stays registered, so a publish still walking it reaches a subscriber added after this
            list.Items = list.Items.Where(entry => entry.Handler.IsAlive && !entry.Handler.Equals(handler)).ToArray();
        }
    }

    public virtual void Dispose()
    {
        lock (subscriptionLock)
        {
            subscribers.Clear();
        }
    }

    /// <summary>
    /// Subscribers of one message type, in registration order
    /// </summary>
    protected sealed class SubscriberList
    {
        private volatile Subscription[] items = Array.Empty<Subscription>();

        // Replaced under subscriptionLock and never edited, so a publish walks the array it read without locking
        public Subscription[] Items
        {
            get => items;
            internal set => items = value;
        }

        public int Count => items.Length;
    }

    /// <summary>
    /// One subscribed handler and the order it was registered in
    /// </summary>
    protected sealed class Subscription
    {
        public Subscription(WeakDelegate handler, long sequence)
        {
            Handler = handler;
            Sequence = sequence;
        }

        public WeakDelegate Handler { get; }

        public long Sequence { get; }
    }
}
