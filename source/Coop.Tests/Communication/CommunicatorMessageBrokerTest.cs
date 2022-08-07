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
            var container = Bootstrap.Initialize(true);
            using var communicator = container.Resolve<IMessageBroker>();

            communicator.Subscribe<ExampleIncomingMessage>(payload => { });
            
            var subscribers = typeof(CommunicatorMessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, List<Delegate>>;

            if (subscribers == null)
                throw new Exception("Subscribers dictionary couldn't not be found.");
            
            Assert.True(subscribers[typeof(ExampleIncomingMessage)].Count == 1);
        }

        [Fact]
        public void UnsubscribeOneEvent()
        {
            var container = Bootstrap.Initialize(true);
            using var communicator = container.Resolve<IMessageBroker>();

            void DelegateHandler(MessagePayload<ExampleIncomingMessage> payload) { }
            communicator.Subscribe<ExampleIncomingMessage>(DelegateHandler);
            
            var subscribers = typeof(CommunicatorMessageBroker).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(communicator) as Dictionary<Type, List<Delegate>>;
            
            if (subscribers == null)
                throw new Exception("Subscribers dictionary couldn't not be found.");

            if (subscribers[typeof(ExampleIncomingMessage)].Count != 1)
                throw new Exception("Subscription failed during the test.");
            
            communicator.Unsubscribe<ExampleIncomingMessage>(DelegateHandler);
            Assert.True(subscribers[typeof(ExampleIncomingMessage)].Count == 0);
        }

        [Fact]
        public void PublishOneEventInternal()
        {
            var container = Bootstrap.Initialize(true);
            using var communicator = container.Resolve<IMessageBroker>();

            var callCount = 0;
            var eventData = 0;
            communicator.Subscribe<ExampleIncomingMessage>(payload => { 
                callCount++;
                eventData = payload.What.ExampleData;
            });

            var incomingMessage = new ExampleIncomingMessage(10);
            communicator.Publish(this, incomingMessage, MessageScope.Internal);
            
            Assert.Equal(1, callCount);
            Assert.Equal(incomingMessage.ExampleData, eventData);
        }
        
        [Fact]
        public void PublishOneEventExternal()
        {
            var container = Bootstrap.Initialize(true);
            using var communicator = container.Resolve<IMessageBroker>();
            
            communicator.Subscribe<ExampleIncomingMessage>(payload => { });

            var incomingMessage = new ExampleIncomingMessage(20);
            communicator.Publish(this, incomingMessage, MessageScope.External);
            
            // Find a way to check if the message has been published correctly.
            Assert.Fail("To implement.");
        }
    }
}