using Common.Messaging;
using System;

namespace Coop.Tests.Stubs
{
    internal class MessageBrokerStub : IMessageBroker
    {
        public void Dispose()
        {
            
        }

        public void Publish<T>(object sender, T message)
        {
            
        }

        public void Subscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            
        }
    }
}
