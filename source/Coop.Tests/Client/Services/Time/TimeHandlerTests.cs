using Common.Messaging;
using Coop.Core.Client.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Client.Services.Time
{
    public class TimeHandlerTests
    {
        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            var broker = new MockMessageBroker();
            var network = new MockNetwork();
            var handler = new TimeHandler(broker, network);

            Assert.NotEmpty(broker.Subscriptions);

            // Act
            handler.Dispose();

            // Assert
            Assert.Empty(broker.Subscriptions);
        }

        [Fact]
        public void TimeSpeedChanged_Publishes_NetworkRequestTimeSpeedChange()
        {
            // Arrange
            var broker = new MockMessageBroker();
            var network = new MockNetwork();
            var handler = new TimeHandler(broker, network);
            var payload = new AttemptedTimeSpeedChanged(CampaignTimeControlMode.StoppablePlay);
            var message = new MessagePayload<AttemptedTimeSpeedChanged>(null, payload);

            var peer = network.CreatePeer();

            // Act
            handler.Handle_TimeSpeedChanged(message);

            // Assert
            var sentMessages = network.GetPeerMessages(peer);
            Assert.Single(sentMessages);
            Assert.IsType<NetworkRequestTimeSpeedChange>(sentMessages.First());
            var networkRequestTimeSpeedChange = (NetworkRequestTimeSpeedChange)sentMessages.First();
            Assert.Equal(message.What.NewControlMode, networkRequestTimeSpeedChange.NewControlMode);
        }

        [Fact]
        public void NetworkTimeSpeedChanged_Publishes_SetTimeControlMode()
        {
            // Arrange
            var broker = new MockMessageBroker();
            var network = new MockNetwork();
            var handler = new TimeHandler(broker, network);
            var payload = new NetworkTimeSpeedChanged(TimeControlEnum.Play_2x);
            var message = new MessagePayload<NetworkTimeSpeedChanged>(null, payload);

            // Act
            handler.Handle_NetworkTimeSpeedChanged(message);

            // Assert
            var timeControlMessage = Assert.Single(broker.PublishedMessages);
            var setTimeControlMode = Assert.IsType<SetTimeControlMode>(timeControlMessage);
            Assert.Equal(payload.NewControlMode, setTimeControlMode.NewTimeMode);
        }
    }
}
