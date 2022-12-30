using System;

namespace Common.Messaging
{
    public interface IMessageBroker : IDisposable
    {
        void Publish<T>(T message);
        void Publish<T>(object source, T message);

        void Subscribe<T>(Action<MessagePayload<T>> subcription);

        void Unsubscribe<T>(Action<MessagePayload<T>> subscription);
    }
}
