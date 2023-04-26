using Common.Messaging;
using System;
using System.Linq;

namespace Coop.Tests.Stubs
{
    public class MessageBrokerStub : MessageBroker
    {
        public int GetTotalSubscribers()
        {
            int total = 0;
            foreach (var type in _subscribers.Keys)
            {
                total += _subscribers[type].Count;
            }

            return total;
        }

        public override void Publish<T>(object source, T message)
        {
            if (!_subscribers.ContainsKey(typeof(T)))
            {
                return;
            }
            Delegate[] delegates = new Delegate[_subscribers[typeof(T)].Count];
            //_subscribers[typeof(T)].CopyTo(delegates, 0); //TODO
            if (delegates == null || delegates.Length == 0) return;
            var payload = new MessagePayload<T>(source, message);
            foreach (var handler in delegates.Select
            (item => item as Action<MessagePayload<T>>))
            {
                handler?.Invoke(payload);
            }
        }
    }
}
