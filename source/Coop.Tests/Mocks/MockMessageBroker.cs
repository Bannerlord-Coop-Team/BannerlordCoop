using Common;
using Common.Messaging;
using LiteNetLib;
using System;
using System.Collections.Generic;

namespace Coop.Tests.Mocks
{
    public class MockMessageBroker : IMessageBroker
    {
        public List<Delegate> Subscriptions { get; } = new List<Delegate>();
        public List<object> PublishedMessages { get; } = new List<object>();

        public void Subscribe<T>(Action<MessagePayload<T>> handler)
        {
            Subscriptions.Add(handler);
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> handler)
        {
            Subscriptions.Remove(handler);
        }

        public void Publish<T>(object sender, T message)
        {
            PublishedMessages.Add(message);
        }

        public void PublishNetworkEvent(object message)
        {
            PublishedMessages.Add(message);
        }

        public void Dispose()
        {
        }
    }
}
