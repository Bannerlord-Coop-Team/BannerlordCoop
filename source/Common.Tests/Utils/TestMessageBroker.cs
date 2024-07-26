using Common.Messaging;

namespace Common.Tests.Utils;

/// <summary>
/// Message broker used for testing. Runs synchronously.
/// </summary>
public class TestMessageBroker : MessageBroker
{
    public readonly MessageCollection Messages = new MessageCollection();

    private readonly Dictionary<Type, List<WeakDelegate>> _subscribers = new Dictionary<Type, List<WeakDelegate>>();

    public override void Publish<T>(object source, T message)
    {
        if (message == null)
            return;

        Messages.Add(message);

        Type messageType = message.GetType();
        if (!_subscribers.ContainsKey(messageType))
        {
            return;
        }

        var delegates = _subscribers[messageType].ToList();
        if (delegates == null || delegates.Count == 0) return;

        var payloadType = typeof(MessagePayload<>).MakeGenericType(messageType);
        object payload = Activator.CreateInstance(payloadType, new object[] { source, message })!;
        for (int i = 0; i < delegates.Count; i++)
        {
            var weakDelegate = delegates[i];
            if (weakDelegate == null || weakDelegate.IsAlive == false)
            {
                // Remove dead delegates
                delegates.RemoveAt(i--);
                continue;
            }

            weakDelegate.Invoke(new object[] { payload });
        }
    }

    public override void Respond<T>(object target, T message)
    {
        if (message == null)
            return;

        var payload = new MessagePayload<T>(target, message);
        Messages.Add(message);

        Type messageType = message.GetType();
        if (!_subscribers.ContainsKey(messageType))
        {
            return;
        }

        var delegates = _subscribers[messageType];
        if (delegates == null || delegates.Count == 0) return;


        for (int i = 0; i < delegates.Count; i++)
        {
            // TODO this might be slow
            var weakDelegate = delegates[i];
            if (weakDelegate == null || weakDelegate.IsAlive == false)
            {
                // Remove dead delegates
                delegates.RemoveAt(i--);
                continue;
            }

            if (weakDelegate.Instance == target)
            {
                weakDelegate.Invoke(new object[] { payload });
                // Can only respond to one source, no longer need to loop if found
                return;
            }
        }
    }

    public override void Subscribe<T>(Action<MessagePayload<T>> subscription)
    {
        var delegates = _subscribers.ContainsKey(typeof(T)) ?
                        _subscribers[typeof(T)] : new List<WeakDelegate>();
        if (!delegates.Contains(subscription))
        {
            delegates.Add(subscription);
        }
        _subscribers[typeof(T)] = delegates;
    }

    public override void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
    {
        if (!_subscribers.ContainsKey(typeof(T))) return;
        var delegates = _subscribers[typeof(T)];
        if (delegates.Contains(new WeakDelegate(subscription)))
            delegates.Remove(subscription);
        if (delegates.Count == 0)
            _subscribers.Remove(typeof(T));
    }

    public override void Dispose()
    {
        _subscribers?.Clear();
    }

    public int GetSubscriberCountForType<T>()
    {
        if (_subscribers.TryGetValue(typeof(T), out var delegates)) return delegates.Count;

        return -1;
    }

    public int GetTotalSubscribers()
    {
        int totalSubscribers = 0;

        foreach (var item in _subscribers.Values)
        {
            totalSubscribers += item.Count;
        }

        return totalSubscribers;
    }

    public int GetMessageCountFromType<T>() where T : IMessage => Messages.Count(m => m.GetType() == typeof(T));

    public IEnumerable<T> GetMessagesFromType<T>() where T : IMessage => Messages.Where(m => m.GetType() == typeof(T)).Select(m => (T)m);

    public void Clear() => Messages.Clear();

    public void SetStaticInstance()
    {
        instance = this;
    }
}
