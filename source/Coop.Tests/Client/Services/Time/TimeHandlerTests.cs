using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using System.Linq;
using Xunit;

namespace Coop.Tests.Client.Services.Time
{
    public class TimeHandlerTests
    {
        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var handler = new TimeHandler(broker, network);

            Assert.True(broker.GetTotalSubscribers() > 0);

            // Act
            handler.Dispose();

            // Assert
            Assert.Equal(0, broker.GetTotalSubscribers());
        }

        [Fact]
        public void TimeSpeedChanged_Publishes_NetworkRequestTimeSpeedChange()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var handler = new TimeHandler(broker, network);
            var payload = new AttemptedTimeSpeedChanged(TimeControlEnum.Play_1x);
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
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var handler = new TimeHandler(broker, network);
            var payload = new NetworkChangeTimeControlMode(TimeControlEnum.Play_2x);
            var message = new MessagePayload<NetworkChangeTimeControlMode>(null, payload);

            // Act
            handler.Handle_NetworkTimeSpeedChanged(message);

            // Assert
            var timeControlMessage = Assert.Single(broker.Messages);
            var setTimeControlMode = Assert.IsType<SetTimeControlMode>(timeControlMessage);
            Assert.Equal(payload.NewControlMode, setTimeControlMode.NewTimeMode);
        }
    }
}
