using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Messaging
{
    public interface IMessageBroker : IDisposable
    {
        void Publish<T>(object source, T message);

        void Subscribe<T>(Action<MessagePayload<T>> subcription);

        void Unsubscribe<T>(Action<MessagePayload<T>> subscription);
    }

    public class MessageBroker : IMessageBroker
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MessageBroker>();
        protected static MessageBroker _instance;
        protected readonly Dictionary<Type, ConcurrentList<WeakDelegate>> _subscribers;
        public static MessageBroker Instance => _instance;

        public MessageBroker()
        {
            _subscribers = new Dictionary<Type, ConcurrentList<WeakDelegate>>();
        }

        public virtual void Publish<T>(object source, T message)
        {
            if (message == null)
                return;

            Logger.Verbose($"Publishing {message.GetType().Name} from {source?.GetType().Name}");

            if (!_subscribers.ContainsKey(typeof(T)))
            {
                return;
            }

            var delegates = _subscribers[typeof(T)];
            if (delegates == null || delegates.Count == 0) return;
            var payload = new MessagePayload<T>(source, message);
            for(int i = 0; i < delegates.Count; i++)
            {
                var weakDelegate = delegates[i];
                if (weakDelegate.IsAlive == false)
                {
                    delegates.RemoveAt(i--);
                    continue;
                }
                
                Task.Factory.StartNew(() => weakDelegate.Invoke(new object[] { payload }));
            }
        }

        public virtual void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            var delegates = _subscribers.ContainsKey(typeof(T)) ?
                            _subscribers[typeof(T)] : new ConcurrentList<WeakDelegate>();
            if (!delegates.Contains(subscription))
            {
                delegates.Add(subscription);
            }
            _subscribers[typeof(T)] = delegates;
        }

        public virtual void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            
            if (!_subscribers.ContainsKey(typeof(T))) return;
            var delegates = _subscribers[typeof(T)];
            if (delegates.Contains(new WeakDelegate(subscription)))
                delegates.Remove(subscription);
            if (delegates.Count == 0)
                _subscribers.Remove(typeof(T));
        }

        public virtual void Dispose()
        {
            _subscribers?.Clear();
        }
    }
}
