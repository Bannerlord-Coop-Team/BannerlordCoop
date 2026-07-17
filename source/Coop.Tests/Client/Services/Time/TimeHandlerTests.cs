using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapEvents;
using Moq;
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
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);

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
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);
            var payload = new TimeSpeedChangedAttempted(TimeControlEnum.Play_1x);
            var message = new MessagePayload<TimeSpeedChangedAttempted>(null, payload);

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
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);
            var payload = new NetworkChangeTimeControlMode(TimeControlEnum.Play_2x);
            var message = new MessagePayload<NetworkChangeTimeControlMode>(null, payload);

            // Act
            handler.Handle_NetworkTimeSpeedChanged(message);

            // Assert
            mockTimeControlInterface.Verify(m => m.ClientSetTimeControl(payload.NewControlMode), Times.Once);
        }

        [Fact]
        public void NetworkMapEventLockChanged_WhenBecomesBlocked_PublishesFastForwardDisabledMessage()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);

            // Act
            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(1)));

            // Assert
            var message = Assert.Single(broker.GetMessagesFromType<SendInformationMessage>());
            Assert.Equal(MapEventTimeControlMessages.FastForwardDisabled, message.Text);
        }

        [Fact]
        public void NetworkMapEventLockChanged_WhenBecomesUnblocked_PublishesFastForwardEnabledMessage()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);

            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(1)));

            // Act
            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(0)));

            // Assert
            Assert.Contains(
                broker.GetMessagesFromType<SendInformationMessage>(),
                m => m.Text == MapEventTimeControlMessages.FastForwardEnabled);
        }

        [Fact]
        public void NetworkMapEventLockChanged_WhenStillBlocked_DoesNotRepeatAnnouncement()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);

            // Act
            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(1)));
            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(2)));

            // Assert
            Assert.Single(
                broker.GetMessagesFromType<SendInformationMessage>(),
                m => m.Text == MapEventTimeControlMessages.FastForwardDisabled);
        }

        [Fact]
        public void TimeSpeedChanged_WhenFastForwardBlockedByMapEvent_ReportsCountAndDoesNotForward()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var peer = network.CreatePeer();
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);

            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(3)));

            // Act
            handler.Handle_TimeSpeedChanged(
                new MessagePayload<TimeSpeedChangedAttempted>(null, new TimeSpeedChangedAttempted(TimeControlEnum.Play_2x)));

            // Assert
            Assert.Contains(
                broker.GetMessagesFromType<SendInformationMessage>(),
                m => m.Text == MapEventTimeControlMessages.FastForwardBlocked(3));
            Assert.False(network.SentNetworkMessages.ContainsKey(peer.Id));
        }

        [Fact]
        public void TimeSpeedChanged_WhenFastForwardBlockedByMapEvent_StillAllowsNormalSpeed()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var network = new TestNetwork();
            var peer = network.CreatePeer();
            var mockTimeControlInterface = new Mock<ITimeControlInterface>();
            var handler = new TimeHandler(broker, network, mockTimeControlInterface.Object);

            handler.Handle_NetworkMapEventLockChanged(
                new MessagePayload<NetworkMapEventLockChanged>(null, new NetworkMapEventLockChanged(1)));

            // Act
            handler.Handle_TimeSpeedChanged(
                new MessagePayload<TimeSpeedChangedAttempted>(null, new TimeSpeedChangedAttempted(TimeControlEnum.Play_1x)));

            // Assert
            var sent = network.GetPeerMessages(peer);
            Assert.Single(sent);
            Assert.IsType<NetworkRequestTimeSpeedChange>(sent.First());
            Assert.Equal(TimeControlEnum.Play_1x, ((NetworkRequestTimeSpeedChange)sent.First()).NewControlMode);
        }
    }
}
