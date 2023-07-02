using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coop.Tests.Stubs
{
    public class StubMessageBroker : MessageBroker
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

        public override IEnumerable<Task> Publish<T>(object? source, T message)
        {
            if (!_subscribers.ContainsKey(typeof(T)))
            {
                return Array.Empty<Task>();
            }
            var delegates = _subscribers[typeof(T)];
            if (delegates == null || delegates.Count == 0) return Array.Empty<Task>();
            var payload = new MessagePayload<T>(source, message);
            for (int i = 0; i < delegates.Count; i++)
            {
                var weakDelegate = delegates[i];
                if (weakDelegate.IsAlive == false)
                {
                    delegates.RemoveAt(i--);
                    continue;
                }

                weakDelegate.Invoke(new object[] { payload });
            }

            return Array.Empty<Task>();
        }
    }
}
