using System;
using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Coop.Communication;
using Coop.Communication.MessageBroker;
using Coop.Mod.EventHandlers;
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
            
            var subscribers = typeof(NetworkMessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, List<Delegate>>;

            if (subscribers == null)
                throw new Exception("Subscribers dictionary couldn't not be found.");
            
            Assert.True(subscribers[typeof(ExampleIncomingMessage)].Count == 1);
        }

        [Fact]
        public void UnsubscribeOneEvent()
        {
            var container = Bootstrap.InitializeAsServer();
            using var communicator = container.Resolve<IMessageBroker>();

            void DelegateHandler(MessagePayload<ExampleIncomingMessage> payload) { }
            communicator.Subscribe<ExampleIncomingMessage>(DelegateHandler);
            
            var subscribers = typeof(NetworkMessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, List<Delegate>>;
            
            if (subscribers == null)
                throw new Exception("Subscribers dictionary couldn't not be found.");

            if (subscribers[typeof(ExampleIncomingMessage)].Count != 1)
                throw new Exception("Subscription failed during the test.");
            
            communicator.Unsubscribe<ExampleIncomingMessage>(DelegateHandler);
            Assert.True(subscribers[typeof(ExampleIncomingMessage)].Count == 0);
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
            
            Assert.Equal(1, callCount);
            Assert.Equal(incomingMessage.ExampleData, eventData);
        }
    }
}