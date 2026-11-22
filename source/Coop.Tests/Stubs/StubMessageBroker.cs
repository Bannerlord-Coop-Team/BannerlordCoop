using Common.Messaging;

namespace Coop.Tests.Stubs
{
    public class StubMessageBroker : MessageBroker
    {
        public int GetTotalSubscribers()
        {
            int total = 0;
            foreach (var type in subscribers.Keys)
            {
                total += subscribers[type].Count;
            }

            return total;
        }

        public override void Publish<T>(object? source, T message)
        {
            if (!subscribers.TryGetValue(typeof(T), out var list)) return;
            if (list.Count == 0) return;
            var payload = new MessagePayload<T>(source, message);
            foreach (var subscription in list.Items)
            {
                if (subscription.Handler.IsAlive == false) continue;

                subscription.Handler.Invoke(new object[] { payload });
            }
        }
    }
}
