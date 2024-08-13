using Common.Logging;
using Common.Messaging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Messaging
{
    public interface IMessageBroker : IDisposable
    {
        void Publish<T>(object source, T message, string subKey = "") where T : IMessage;

        void Respond<T>(object target, T message, string subKey = "") where T : IResponse;

        void Subscribe<T>(Action<MessagePayload<T>> subscription, string subKey = "") where T : IMessage;

        void Unsubscribe<T>(Action<MessagePayload<T>> subscription, string subKey = "") where T : IMessage;
    }

    public class MessageBroker : IMessageBroker
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MessageBroker>();
        protected static MessageBroker instance;
        protected readonly Dictionary<string, List<WeakDelegate>> subscribers;
        private readonly MessageLogger messageLogger = new MessageLogger(Logger);
        public static MessageBroker Instance { 
            get
            {
                if( instance == null)
                {
                    instance = new MessageBroker();
                }
                return instance;
            } 
        }

        public MessageBroker()
        {
            subscribers = new Dictionary<string, List<WeakDelegate>>();
        }

        public virtual void Publish<T>(object source, T message, string subKey = "") where T : IMessage
        {
            if (message == null)
                return;

            var key = CreateKey(typeof(T), subKey);
            var msgType = message.GetType();

            messageLogger.LogMessage(source, msgType);

            if (!subscribers.TryGetValue(key, out var delegates))
            {
                return;
            }

            if (delegates == null || delegates.Count == 0) return;
            var payload = new MessagePayload<T>(source, message, subKey);
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

                Task.Factory.StartNew(() => weakDelegate.Invoke(new object[] { payload }));
            }
        }

        public virtual void Respond<T>(object target, T message, string subKey = "") where T : IResponse
        {
            if (message == null)
                return;

            var key = CreateKey(typeof(T), subKey);
            Logger.Verbose($"Responding {message.GetType().Name} to {target?.GetType().Name}");

            if (!subscribers.TryGetValue(key, out var delegates))
            {
                return;
            }

            if (delegates == null || delegates.Count == 0) return;
            var payload = new MessagePayload<T>(target, message, subKey);
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

                if (ReferenceEquals(weakDelegate.Instance, target))
                {
                    Task.Factory.StartNew(() => weakDelegate.Invoke(new object[] { payload }));
                    // Can only respond to one source, no longer need to loop if found
                    return;
                }
            }
        }

        public virtual void Subscribe<T>(Action<MessagePayload<T>> subscription, string subKey = "") where T : IMessage
        {
            var key = CreateKey(typeof(T), subKey);
            
            var delegates = subscribers.TryGetValue(key, out var subscriber) ?
                subscriber : new List<WeakDelegate>();
            if (!delegates.Contains(subscription))
            {
                delegates.Add(subscription);
            }
            subscribers[key] = delegates;
        }

        public virtual void Unsubscribe<T>(Action<MessagePayload<T>> subscription, string subKey = "") where T : IMessage
        {
            var key = CreateKey(typeof(T), subKey);

            if (!subscribers.TryGetValue(key, out var delegates)) return;
            if (delegates.Contains(new WeakDelegate(subscription)))
                delegates.Remove(subscription);
            if (delegates.Count == 0)
                subscribers.Remove(key);
        }

        public virtual void Dispose()
        {
            subscribers?.Clear();
        }
        
        public static string CreateKey(Type type, string subKey)
        {
            return $"{type.Name}_{subKey}";
        }
    }
}
