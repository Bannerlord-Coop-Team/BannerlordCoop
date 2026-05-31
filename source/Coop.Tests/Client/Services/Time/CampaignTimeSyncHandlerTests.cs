using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Time.Interfaces;
using Moq;
using Xunit;

namespace Coop.Tests.Client.Services.Time
{
    public class CampaignTimeSyncHandlerTests
    {
        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var mockMapTimeTrackerInterface = new Mock<IMapTimeTrackerInterface>();
            var handler = new CampaignTimeSyncHandler(broker, mockMapTimeTrackerInterface.Object);

            Assert.True(broker.GetTotalSubscribers() > 0);

            // Act
            handler.Dispose();

            // Assert
            Assert.Equal(0, broker.GetTotalSubscribers());
        }

        [Fact]
        public void CampaignTimeUpdated_Calls_SyncCampaignTime()
        {
            // Arrange
            var broker = new TestMessageBroker();
            var mockMapTimeTrackerInterface = new Mock<IMapTimeTrackerInterface>();
            var handler = new CampaignTimeSyncHandler(broker, mockMapTimeTrackerInterface.Object);
            var payload = new CampaignTimeUpdated(123456L);
            var message = new MessagePayload<CampaignTimeUpdated>(null, payload);

            // Act
            handler.Handle_CampaignTimeUpdated(message);

            // Assert
            mockMapTimeTrackerInterface.Verify(m => m.SyncCampaignTime(payload.ServerTicks), Times.Once);
        }
    }
}
