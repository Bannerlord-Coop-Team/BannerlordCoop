using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Time.Enum;
using GameInterface.Services.Time.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Server.Services.Time
{
    public class TimeHandlerTests
    {
        public class MockNetworkMessageBroker : INetworkMessageBroker
        {
            public List<Delegate> Subscriptions { get; } = new List<Delegate>();
            public List<object> PublishedMessages { get; } = new List<object>();
            public List<object> PublishedNetworkMessages { get; } = new List<object>();

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

            public void PublishNetworkEvent(INetworkEvent networkEvent)
            {
                PublishedNetworkMessages.Add(networkEvent);
            }

            public void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent)
            {
                PublishNetworkEvent(networkEvent);
            }

            public void Dispose()
            {
            }
        }

        [Fact]
        public void Constructor_SubscribesToMessageBroker()
        {
            // Arrange
            var broker = new MockNetworkMessageBroker();

            // Act
            var handler = new TimeHandler(broker);

            // Assert
            Assert.Equal(2, broker.Subscriptions.Count);
        }

        [Fact]
        public void Dispose_UnsubscribesFromMessageBroker()
        {
            // Arrange
            var broker = new MockNetworkMessageBroker();
            var handler = new TimeHandler(broker);

            // Act
            handler.Dispose();

            // Assert
            Assert.Empty(broker.Subscriptions);
        }

        [Fact]
        public void Handle_NetworkRequestTimeSpeedChange_PublishesSetTimeControlMode()
        {
            // Arrange
            var broker = new MockNetworkMessageBroker();
            var handler = new TimeHandler(broker);
            var payload = new NetworkRequestTimeSpeedChange(TimeControlEnum.Pause);
            var message = new MessagePayload<NetworkRequestTimeSpeedChange>(null, payload);

            // Act
            handler.Handle_NetworkRequestTimeSpeedChange(message);

            // Assert
            Assert.Single(broker.PublishedMessages);
            Assert.IsType<SetTimeControlMode>(broker.PublishedMessages[0]);
            var setTimeControlMode = (SetTimeControlMode)broker.PublishedMessages[0];
            Assert.Equal(Guid.Empty, setTimeControlMode.TransactionID);
            Assert.Equal(payload.NewControlMode, setTimeControlMode.NewTimeMode);
        }

        [Fact]
        public void Handle_TimeSpeedChanged_PublishesNetworkTimeSpeedChanged()
        {
            // Arrange
            var broker = new MockNetworkMessageBroker();
            var handler = new TimeHandler(broker);
            var message = new MessagePayload<TimeSpeedChanged>(null, new TimeSpeedChanged());

            // Act
            handler.Handle_TimeSpeedChanged(message);

            // Assert
            Assert.Single(broker.PublishedNetworkMessages);
            Assert.IsType<NetworkTimeSpeedChanged>(broker.PublishedNetworkMessages[0]);
        }
    }
}
