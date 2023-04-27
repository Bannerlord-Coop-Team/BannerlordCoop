using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Autofac;
using Common;
using Common.Messaging;
using Xunit;

namespace Coop.Tests.Communication
{
    public class CommunicatorMessageBrokerTest
    {
        [Fact]
        public void SubscribeOneEvent()
        {
            var container = Bootstrap.InitializeAsServer();
            using var communicator = container.Resolve<IMessageBroker>();

            communicator.Subscribe<ExampleIncomingMessage>(payload => { });
            
            var subscribers = typeof(MessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, ConcurrentList<WeakDelegate>>;

            if (subscribers == null)
                throw new Exception("Subscribers dictionary couldn't not be found.");
            
            Assert.True(subscribers[typeof(ExampleIncomingMessage)].Count == 1);
        }

        [Fact]
        public void UnsubscribeOneEvent()
        {
            var container = Bootstrap.InitializeAsServer();
            using var communicator = container.Resolve<IMessageBroker>();

            // We need 2 so the key of ExampleIncomingMessage does not get removed in MessageBroker
            void DelegateHandler(MessagePayload<ExampleIncomingMessage> payload) { }
            void DelegateHandler2(MessagePayload<ExampleIncomingMessage> payload) { }
            communicator.Subscribe<ExampleIncomingMessage>(DelegateHandler);
            communicator.Subscribe<ExampleIncomingMessage>(DelegateHandler2);

            var subscribers = typeof(MessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, List<Delegate>>;

            // Ensure subscribers dictionary was initialized
            Assert.NotNull(subscribers);

            // Ensure ExampleIncomingMessage key has 2 handlers
            Assert.Equal(2, subscribers[typeof(ExampleIncomingMessage)].Count);
            
            communicator.Unsubscribe<ExampleIncomingMessage>(DelegateHandler);

            // Ensure ExampleIncomingMessage key now has 1 handler
            Assert.Single(subscribers[typeof(ExampleIncomingMessage)]);
        }

        [Fact]
        public void UnsubscribeAllEvents()
        {
            var container = Bootstrap.InitializeAsServer();
            using var communicator = container.Resolve<IMessageBroker>();

            // Add handler to message broker
            void DelegateHandler(MessagePayload<ExampleIncomingMessage> payload) { }
            communicator.Subscribe<ExampleIncomingMessage>(DelegateHandler);

            var subscribers = typeof(MessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, List<Delegate>>;

            // Ensure subscribers dictionary was initialized
            Assert.NotNull(subscribers);

            // Ensure ExampleIncomingMessage key has 2 handlers
            Assert.Single(subscribers[typeof(ExampleIncomingMessage)]);

            communicator.Unsubscribe<ExampleIncomingMessage>(DelegateHandler);

            // Ensure ExampleIncomingMessage key is now removed
            Assert.Empty(subscribers);
        }

        [Fact]
        public void PublishOneMessage()
        {
            var container = Bootstrap.InitializeAsServer();
            using var communicator = container.Resolve<IMessageBroker>();

            var callCount = 0;
            var eventData = 0;
            communicator.Subscribe<ExampleIncomingMessage>(payload => { 
                callCount++;
                eventData = payload.What.ExampleData;
            });

            var incomingMessage = new ExampleIncomingMessage(10);
            communicator.Publish(this, incomingMessage);

            Thread.Sleep(10);
            
            Assert.Equal(1, callCount);
            Assert.Equal(incomingMessage.ExampleData, eventData);
        }
    }

    internal readonly struct ExampleIncomingMessage
    {
        public int ExampleData { get; }

        public ExampleIncomingMessage(int ExampleData)
        {
            this.ExampleData = ExampleData;
        }
    }
}