using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time.Handlers;
using GameInterface.Services.Time.Interfaces;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
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
            .Setup(n => n.Send(It.IsAny<NetPeer>(), It.IsAny<IPacket>()))
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

        var connection = new Mock<IConnectionLogic>();
        IEnumerable<IConnectionLogic> connections = new[] { connection.Object };
        var connectionCollection = new Mock<IConnectionCollection>();
        connectionCollection
            .Setup(c => c.GetEnumerator())
            .Returns(() => connections.GetEnumerator());
        var connectionMessageQueue = new Mock<IConnectionMessageQueue>();

        var handler = new CampaignTimeSyncHandler(
            network.Object,
            mapTimeTracker.Object,
            connectionCollection.Object,
            connectionMessageQueue.Object);

        Assert.True(sendStarted.Wait(TimeSpan.FromSeconds(5)));

        Exception disposeException = null;
        var disposeThread = new Thread(() =>
        {
            try
            {
                disposeStarted.Set();
                handler.Dispose();
            }
            catch (Exception ex)
            {
                disposeException = ex;
            }
        });
        disposeThread.Start();

        try
        {
            Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(disposeThread.Join(TimeSpan.FromMilliseconds(250)));
        }
        finally
        {
            releaseSend.Set();
            Assert.True(disposeThread.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.Null(disposeException);
    }
}
