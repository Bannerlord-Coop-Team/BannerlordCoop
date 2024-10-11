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
        void Publish<T>(object source, T message) where T : IMessage;

        void Respond<T>(object target, T message) where T : IResponse;

        void Subscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage;

        void Unsubscribe<T>(Action<MessagePayload<T>> subscription) where T : IMessage;
    }

    public class MessageBroker : IMessageBroker
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MessageBroker>();
        protected static MessageBroker instance;
        protected readonly Dictionary<Type, List<WeakDelegate>> subscribers;
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
            subscribers = new Dictionary<Type, List<WeakDelegate>>();
        }

        public virtual void Publish<T>(object source, T message) where T : IMessage
        {
            if (message == null)
                return;

            var msgType = message.GetType();

            messageLogger.LogMessage(source, msgType);

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

                // Making synchronous to maintain sequencing of packets
                //Task.Factory.StartNew(() => weakDelegate.Invoke(new object[] { payload }));

                weakDelegate.Invoke(new object[] { payload });
            }
        }

        public virtual void Respond<T>(object target, T message) where T : IResponse
        {
            if (message == null)
                return;

            Logger.Verbose($"Responding {message.GetType().Name} to {target?.GetType().Name}");

            if (!subscribers.ContainsKey(typeof(T)))
            {
                return;
            }

            var delegates = subscribers[typeof(T)];
            if (delegates == null || delegates.Count == 0) return;
            var payload = new MessagePayload<T>(target, message);
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
}
