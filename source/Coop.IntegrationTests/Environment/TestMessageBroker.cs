using Common.Messaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Coop.IntegrationTests.Environment
{
    public class TestMessageBroker : IMessageBroker
    {
        public readonly MessageCollection Messages = new MessageCollection();

        private readonly Dictionary<Type, List<WeakDelegate>> _subscribers;
        public TestMessageBroker()
        {
            _subscribers = new Dictionary<Type, List<WeakDelegate>>();
        }

        private readonly ConstructorInfo ctor_payload = typeof(MessagePayload<>)
                .GetConstructors().First();

        public virtual void Publish<T>(object source, T message) where T : IMessage
        {
            Messages.Add(message);

            if (message == null)
                return;

            Type messageType = message.GetType();
            if (!_subscribers.ContainsKey(messageType))
            {
                return;
            }

            var delegates = _subscribers[messageType];
            if (delegates == null || delegates.Count == 0) return;

            Type t = typeof(MessagePayload<>).MakeGenericType(messageType);

            object payload = Activator.CreateInstance(t, source, message)!;
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

                weakDelegate.Invoke(new object[] { payload });
            }
        }

        public void Respond<T>(object target, T message) where T : IResponse
        {
            Messages.Add(message);

            if (message == null)
                return;

            Type messageType = message.GetType();
            if (!_subscribers.ContainsKey(messageType))
            {
                return;
            }

            var delegates = _subscribers[messageType];
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
                    weakDelegate.Invoke(new object[] { payload });
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
