using Common.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Common.Messaging
{
    public interface IMessageBroker : IDisposable
    {
        void Publish<T>(object source, T message) where T : IMessage;

        void Respond<T>(object target, T message) where T : IResponse;

        void Subscribe<T>(Action<MessagePayload<T>> subscription);

        void Unsubscribe<T>(Action<MessagePayload<T>> subscription);
    }

    public class MessageBroker : IMessageBroker
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MessageBroker>();
        protected static MessageBroker _instance;
        protected readonly Dictionary<Type, List<WeakDelegate>> _subscribers;
        public static MessageBroker Instance { 
            get
            {
                if( _instance == null)
                {
                    _instance = new MessageBroker();
                }
                return _instance;
            } 
        } 
            
        

        public MessageBroker()
        {
            _subscribers = new Dictionary<Type, List<WeakDelegate>>();
        }

        public virtual void Publish<T>(object source, T message) where T : IMessage
        {
            if (message == null)
                return;

            var msgType = message.GetType();

            if (msgType.GetCustomAttribute<DontLogMessageAttribute>() == null)
            {
                Logger.Verbose("Publishing {msgName} from {sourceName}", msgType.Name, source?.GetType().Name);
            }
            else
            {
                LogMessage(msgType);
            }


            if (!_subscribers.ContainsKey(typeof(T)))
            {
                return;
            }

            var delegates = _subscribers[typeof(T)];
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

                Task.Factory.StartNew(() => weakDelegate.Invoke(new object[] { payload }));
            }
        }

        private ConcurrentDictionary<Type, BatchLogger> _loggers = new ConcurrentDictionary<Type, BatchLogger>();
        private void LogMessage(Type messageType)
        {
            if (_loggers.TryGetValue(messageType, out var batchLogger))
            {
                batchLogger.LogOne();
            }
            else
            {
                var newBatchLogger = new BatchLogger(messageType.Name, Serilog.Events.LogEventLevel.Verbose);
                if(_loggers.TryAdd(messageType, newBatchLogger))
                {
                    Logger.Error("Unable to add {messageType} to batch loggers");
                    return;
                }

                newBatchLogger.LogOne();
            }
        }

        public void Respond<T>(object target, T message) where T : IResponse
        {
            if (message == null)
                return;

            Logger.Verbose($"Responding {message.GetType().Name} to {target?.GetType().Name}");

            if (!_subscribers.ContainsKey(typeof(T)))
            {
                return;
            }

            var delegates = _subscribers[typeof(T)];
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

                if (weakDelegate.Instance == target)
                {
                    Task.Factory.StartNew(() => weakDelegate.Invoke(new object[] { payload }));
                    // Can only respond to one source, no longer need to loop if found
                    return;
                }
            }
        }

        public virtual void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            var delegates = _subscribers.ContainsKey(typeof(T)) ?
                            _subscribers[typeof(T)] : new List<WeakDelegate>();
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
