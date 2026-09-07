using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

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
    // Publishes currently running on this broker, counted with Interlocked because the network thread and the game thread both publish
    private int publishDepth;
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
        Interlocked.Increment(ref publishDepth);
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
            Interlocked.Decrement(ref publishDepth);
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
    // A running publish walks its list by index, so removing an entry it already passed would make it
    // skip the next live subscriber; that list is left alone until the publish is done.
    private void RemoveDeadSubscribers(List<WeakDelegate> delegates)
    {
        if (publishDepth > 0) return;
        delegates.RemoveAll(weakDelegate => !weakDelegate.IsAlive);
    }

    public virtual void Dispose()
    {
        subscribers?.Clear();
    }
}
