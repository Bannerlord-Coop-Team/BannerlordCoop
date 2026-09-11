using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;

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
    protected readonly Dictionary<Type, List<WeakDelegate>> subscribers;
    // Guards the two lists below. They replace an Interlocked counter, and a counter cannot be left
    // unbalanced by an interleaving, while a lost list update would mark a list as walked for good
    // and stop pruning it for the lifetime of the broker
    private readonly object bookkeepingLock = new object();
    // Subscriber lists a publish is currently walking, one entry per running publish, so pruning a
    // list holds off while a loop is stepping through it by index
    private readonly List<List<WeakDelegate>> walkedByPublish = new List<List<WeakDelegate>>();
    // Lists whose pruning was skipped for that reason, caught up by the publish that leaves last
    private readonly List<List<WeakDelegate>> prunePending = new List<List<WeakDelegate>>();
    public static MessageBroker Instance { 
        get
        {
            instance ??= new MessageBroker();
            return instance;
        } 
    }

    public MessageBroker()
    {
        subscribers = new Dictionary<Type, List<WeakDelegate>>();
    }

    public virtual void Publish<T>(object source, T message) where T : IMessage
    {
        if (message == null)
            return;

        if (!subscribers.ContainsKey(typeof(T)))
        {
            return;
        }

        var delegates = subscribers[typeof(T)];
        if (delegates == null || delegates.Count == 0) return;
        var payload = new MessagePayload<T>(source, message);
        lock (bookkeepingLock)
        {
            walkedByPublish.Add(delegates);
        }
        try
        {
            for (int i = 0; i < delegates.Count; i++)
            {
                // TODO this might be slow
                var weakDelegate = delegates[i];
                if (weakDelegate.IsAlive == false)
                {
                    // Subscriptions are weak by design, but a collected target means this message is
                    // silently not handled — name it, because a lost handler is otherwise invisible
                    // (the classic trap: a closure/lambda subscription nothing kept alive).
                    Logger.Warning("Dropping dead subscriber {Method} for {MessageType}: its target was garbage collected",
                        weakDelegate.Method?.Name ?? "<unknown>", typeof(T).Name);
                    delegates.RemoveAt(i--);
                    continue;
                }

                try
                {
                    // Making synchronous to maintain sequencing of packets
                    weakDelegate.Invoke(new object[] { payload });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to run {Method}", (weakDelegate.Instance as WeakDelegate)?.Method.Name ?? "<null>");
                }
            }
        }
        finally
        {
            lock (bookkeepingLock)
            {
                walkedByPublish.Remove(delegates);
                if (!walkedByPublish.Contains(delegates) && prunePending.Remove(delegates))
                {
                    delegates.RemoveAll(weakDelegate => !weakDelegate.IsAlive);
                    if (delegates.Count == 0 && subscribers.TryGetValue(typeof(T), out var current) && current == delegates)
                        subscribers.Remove(typeof(T));
                }
            }
        }
    }

    public virtual void Subscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage
    {
        var delegates = subscribers.ContainsKey(typeof(T)) ?
                        subscribers[typeof(T)] : new List<WeakDelegate>();
        RemoveDeadSubscribers(delegates);
        if (!delegates.Contains(subscription))
        {
            delegates.Add(subscription);
        }
        subscribers[typeof(T)] = delegates;
    }

    public virtual void Unsubscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage
    {
        
        if (!subscribers.ContainsKey(typeof(T))) return;
        var delegates = subscribers[typeof(T)];
        if (delegates.Contains(new WeakDelegate(subscription)))
            delegates.Remove(subscription);
        RemoveDeadSubscribers(delegates);
        if (delegates.Count == 0)
            subscribers.Remove(typeof(T));
    }

    // Entries whose target was collected otherwise sit here until this message type is published again.
    // Pruning a list that a publish is stepping through by index would move entries past its position,
    // so that list is noted here and pruned in the finally of that publish instead.
    private void RemoveDeadSubscribers(List<WeakDelegate> delegates)
    {
        lock (bookkeepingLock)
        {
            if (walkedByPublish.Contains(delegates))
            {
                if (!prunePending.Contains(delegates))
                    prunePending.Add(delegates);
                return;
            }
            delegates.RemoveAll(weakDelegate => !weakDelegate.IsAlive);
        }
    }

    public virtual void Dispose()
    {
        subscribers?.Clear();
    }
}
