using Coop.Steam;
using System;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamMissionBridgeTests
    {
        [Fact]
        public void ClosedSubscriberThrows_StillSchedulesOutgoingClientDisposal()
        {
            var hostTransport = new FakeSteamTunnelTransport();
            var clientTransport = new FakeSteamTunnelTransport();
            Action? cleanup = null;

            using var bridge = new SteamMissionBridge(
                localSteamId: 2,
                hostTransport,
                () => clientTransport,
                scheduledCleanup => cleanup = scheduledCleanup);

            bridge.Start(4200);
            Assert.True(bridge.TryConnect(remoteSteamId: 1, out _));

            bridge.PeerDisconnected += _ => throw new InvalidOperationException("scripted subscriber failure");

            Assert.Throws<InvalidOperationException>(() =>
                clientTransport.RaiseConnectionState(
                    clientTransport.NextConnection,
                    TunnelConnectionState.Closed));

            Assert.NotNull(cleanup);
            cleanup!();

            Assert.True(clientTransport.Disposed);
            Assert.Contains(clientTransport.NextConnection, clientTransport.ClosedConnections);
        }
    }
}
