using Common;
using Common.Messaging;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coop.Tests.Mocks
{
    public class MockMessageBroker : IMessageBroker
    {
        public List<Delegate> Subscriptions { get; } = new List<Delegate>();
        public List<IMessage> PublishedMessages { get; } = new List<IMessage>();
        public List<IResponse> Responses { get; } = new List<IResponse>();

        public void Subscribe<T>(Action<MessagePayload<T>> handler)
        {
            Subscriptions.Add(handler);
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> handler)
        {
            Subscriptions.Remove(handler);
        }

        public Task[] Publish<T>(object sender, T message) where T : IMessage
        {
            PublishedMessages.Add(message);

            return Array.Empty<Task>();
        }
        public void Respond<T>(object source, T message) where T : IResponse
        {
            PublishedMessages.Add(message);
        }

        public void Dispose()
        {
        }
    }
}
