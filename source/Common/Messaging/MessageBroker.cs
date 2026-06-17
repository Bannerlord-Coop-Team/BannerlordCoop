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
        for (int i = 0; i < delegates.Count; i++)
        {
            // TODO this might be slow
            var weakDelegate = delegates[i];
            if (weakDelegate.IsAlive == false)
            {
                // Remove dead delegates
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

    public virtual void Subscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage
    {
        var delegates = subscribers.ContainsKey(typeof(T)) ?
                        subscribers[typeof(T)] : new List<WeakDelegate>();
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
        if (delegates.Count == 0)
            subscribers.Remove(typeof(T));
    }

    public virtual void Dispose()
    {
        subscribers?.Clear();
    }
}
