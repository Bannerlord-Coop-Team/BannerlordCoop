using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Time.Handlers;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.Time.Interfaces;
using Moq;
using Xunit;

namespace Coop.Tests.Client.Services.Time;

public class CampaignTimePacketHandlerTests
{
    [Fact]
    public void Dispose_RemovesPacketHandler()
    {
        var broker = new TestMessageBroker();
        var packetManager = new Mock<IPacketManager>();
        var mapTimeTracker = new Mock<IMapTimeTrackerInterface>();
        var handler = new CampaignTimePacketHandler(broker, packetManager.Object, mapTimeTracker.Object);

        handler.Dispose();

        packetManager.Verify(m => m.RemovePacketHandler(handler), Times.Once);
    }

    [Fact]
    public void CampaignTimePacket_CallsSyncCampaignTime()
    {
        var broker = new TestMessageBroker();
        var packetManager = new Mock<IPacketManager>();
        var mapTimeTracker = new Mock<IMapTimeTrackerInterface>();
        var handler = new CampaignTimePacketHandler(broker, packetManager.Object, mapTimeTracker.Object);
        var packet = new CampaignTimePacket(123456L, -1);

        handler.HandlePacket(null, packet);

        mapTimeTracker.Verify(m => m.SyncCampaignTime(packet.ServerTicks, 0f), Times.Once);
        var sample = Assert.Single(broker.GetMessagesFromType<CampaignTimeSampleReceived>());
        Assert.Equal(-1, sample.JoinPacketsRemaining);
    }

    [Fact]
    public void CampaignTimePacket_WithJoinProgress_PublishesRemainingPackets()
    {
        var broker = new TestMessageBroker();
        var packetManager = new Mock<IPacketManager>();
        var mapTimeTracker = new Mock<IMapTimeTrackerInterface>();
        var handler = new CampaignTimePacketHandler(broker, packetManager.Object, mapTimeTracker.Object);

        handler.HandlePacket(null, new CampaignTimePacket(123456L, 4321));

        var sample = Assert.Single(broker.GetMessagesFromType<CampaignTimeSampleReceived>());
        Assert.Equal(4321, sample.JoinPacketsRemaining);
    }

    [Theory]
    [InlineData(-1, 0f)]
    [InlineData(75, 0.075f)]
    [InlineData(500, 0.25f)]
    public void CalculateOneWayLatencySeconds_ClampsNetworkLatency(int latencyMilliseconds, float expectedSeconds)
    {
        Assert.Equal(expectedSeconds, CampaignTimePacketHandler.CalculateOneWayLatencySeconds(latencyMilliseconds));
    }
}
