using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Messages
{
    public class MessageBroker : IMessageBroker
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        ///     Call an event based on the type of the message.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="message">Message event</param>
        /// <param name="scope">Scope of the message</param>
        /// <typeparam name="T">Type of the message</typeparam>
        public virtual void Publish<T>(object source, T message)
        {
            if (message == null || source == null)
                return;
            if (!_subscribers.ContainsKey(typeof(T)))
            {
                return;
            }
            var delegates = _subscribers[typeof(T)];
            if (delegates == null || delegates.Count == 0) return;
            var payload = new MessagePayload<T>(source, message);
            foreach (var handler in delegates.Select
            (item => item as Action<MessagePayload<T>>))
            {
                Task.Factory.StartNew(() => handler?.Invoke(payload));
            }
        }

        /// <summary>
        ///     Register an delegate based on the type of T so that it get called
        ///     when we receive an event of T.
        /// </summary>
        /// <param name="subscriber">Delegate method</param>
        /// <typeparam name="T">Type of event subscribing</typeparam>
        public virtual void Subscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            if (!_subscribers.ContainsKey(typeof(T)))
                _subscribers.Add(typeof(T), new List<Delegate>());

            _subscribers[typeof(T)].Add(subscriber);
        }

        /// <summary>
        ///     Unregister an event delegate.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <typeparam name="T"></typeparam>
        public virtual void Unsubscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            if (_subscribers.TryGetValue(typeof(T), out var subscribers))
                subscribers.Remove(subscriber);
        }

        public virtual void Dispose()
        {
            _subscribers.Clear();
        }
    }
}
