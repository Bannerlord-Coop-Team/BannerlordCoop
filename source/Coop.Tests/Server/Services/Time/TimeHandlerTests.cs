using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Server.Services.Time
{
    public class TimeHandlerTests
    {
        [Fact]
        public void Dispose_UnsubscribesFromMessageBroker()
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
        public void NetworkRequestTimeSpeedChange_Publishes_SetTimeControlMode()
        {
            // Arrange
            var broker = new MockMessageBroker();
            var network = new MockNetwork();
            var handler = new TimeHandler(broker, network);
            var payload = new NetworkRequestTimeSpeedChange(TimeControlEnum.Pause);
            var message = new MessagePayload<NetworkRequestTimeSpeedChange>(null, payload);

            // Act
            handler.Handle_NetworkRequestTimeSpeedChange(message);

            // Assert
            Assert.Single(broker.PublishedMessages);
            Assert.IsType<SetTimeControlMode>(broker.PublishedMessages[0]);
            var setTimeControlMode = (SetTimeControlMode)broker.PublishedMessages[0];
            Assert.Equal(payload.NewControlMode, setTimeControlMode.NewTimeMode);
        }

        [Fact]
        public void TimeSpeedChanged_Publishes_NetworkTimeSpeedChanged()
        {
            // Arrange
            var broker = new MockMessageBroker();
            var network = new MockNetwork();
            var handler = new TimeHandler(broker, network);
            var message = new MessagePayload<TimeSpeedChanged>(null, new TimeSpeedChanged(CampaignTimeControlMode.StoppablePlay));

            network.CreatePeer();

            // Act
            handler.Handle_TimeSpeedChanged(message);

            // Assert
            Assert.NotEmpty(network.Peers);
            foreach(var peer in network.Peers)
            {
                var speedChangedMessage = Assert.Single(network.GetPeerMessages(peer));
                Assert.IsType<NetworkTimeSpeedChanged>(speedChangedMessage);
            }
        }
    }
}
