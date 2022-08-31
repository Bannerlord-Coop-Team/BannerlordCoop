using System;

namespace Common.Messaging
{
    public interface IMessageBroker : IDisposable
    {
        void Subscribe<T>(Action<MessagePayload<T>> subscriber);
        void Unsubscribe<T>(Action<MessagePayload<T>> subscriber);
        void Publish<T>(object sender, T message);
    }
}
