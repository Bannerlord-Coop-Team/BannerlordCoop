using Common.Messaging;
using Coop.Core.Client.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Time.Enum;
using GameInterface.Services.Time.Messages;
using System;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Client.Services.Time
{
    public class TimeHandlerTests
    {
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
        public void Handle_TimeSpeedChanged_PublishesNetworkRequestTimeSpeedChange()
        {
            // Arrange
            var broker = new MockNetworkMessageBroker();
            var handler = new TimeHandler(broker);
            var payload = new TimeSpeedChanged(CampaignTimeControlMode.StoppablePlay);
            var message = new MessagePayload<TimeSpeedChanged>(null, payload);

            // Act
            handler.Handle_TimeSpeedChanged(message);

            // Assert
            Assert.Single(broker.PublishedNetworkMessages);
            Assert.IsType<NetworkRequestTimeSpeedChange>(broker.PublishedNetworkMessages[0]);
            var networkRequestTimeSpeedChange = (NetworkRequestTimeSpeedChange)broker.PublishedNetworkMessages[0];
            Assert.Equal(message.What.NewControlMode, networkRequestTimeSpeedChange.NewControlMode);
        }

        [Fact]
        public void Handle_NetworkTimeSpeedChanged_PublishesSetTimeControlMode()
        {
            // Arrange
            var broker = new MockNetworkMessageBroker();
            var handler = new TimeHandler(broker);
            var payload = new NetworkTimeSpeedChanged(TimeControlEnum.Play_2x);
            var message = new MessagePayload<NetworkTimeSpeedChanged>(null, payload);

            // Act
            handler.Handle_NetworkTimeSpeedChanged(message);

            // Assert
            Assert.Single(broker.PublishedMessages);
            Assert.IsType<SetTimeControlMode>(broker.PublishedMessages[0]);
            var setTimeControlMode = (SetTimeControlMode)broker.PublishedMessages[0];
            Assert.Equal(Guid.Empty, setTimeControlMode.TransactionID);
            Assert.Equal(payload.NewControlMode, setTimeControlMode.NewTimeMode);
        }
    }
}
