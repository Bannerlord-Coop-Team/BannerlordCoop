using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Server.Services.Time.Handlers;
using GameInterface.Services.Time.Interfaces;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Server.Services.Time;

public class CampaignTimeSyncHandlerTests
{
    [Fact]
    public void Dispose_WaitsForInFlightBroadcast()
    {
        using var sendStarted = new ManualResetEventSlim();
        using var releaseSend = new ManualResetEventSlim();
        using var disposeStarted = new ManualResetEventSlim();

        var network = new Mock<INetwork>();
        network
            .Setup(n => n.SendAll(It.IsAny<IPacket>()))
            .Callback(() =>
            {
                sendStarted.Set();
                releaseSend.Wait();
            });

        long currentTicks = 123456L;
        var mapTimeTracker = new Mock<IMapTimeTrackerInterface>();
        mapTimeTracker
            .Setup(m => m.TryGetCurrentTicks(out currentTicks))
            .Returns(true);

        var handler = new CampaignTimeSyncHandler(network.Object, mapTimeTracker.Object);

        Assert.True(sendStarted.Wait(TimeSpan.FromSeconds(5)));

        var disposeTask = Task.Run(() =>
        {
            disposeStarted.Set();
            handler.Dispose();
        });

        try
        {
            Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(disposeTask.Wait(TimeSpan.FromMilliseconds(250)));
        }
        finally
        {
            releaseSend.Set();
        }

        Assert.True(disposeTask.Wait(TimeSpan.FromSeconds(5)));
    }
}
