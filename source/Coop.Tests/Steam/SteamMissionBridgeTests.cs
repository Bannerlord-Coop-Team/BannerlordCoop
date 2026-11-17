using Common.Network.Session;
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
                scheduledCleanup => cleanup = scheduledCleanup, () => new Common.Logging.ReceivePathDiagnostics());

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

        [Fact]
        public void TryGetRouteHealth_ReadsTheConnectionOnEitherSideOfThePeerRole()
        {
            const ulong dialedPeer = 1;
            const ulong acceptedPeer = 3;
            const uint acceptedConnection = 7;
            var hostTransport = new FakeSteamTunnelTransport();
            var clientTransport = new FakeSteamTunnelTransport();
            using var bridge = new SteamMissionBridge(
                localSteamId: 2,
                hostTransport,
                () => clientTransport,
                cleanup => { },
                () => new Common.Logging.ReceivePathDiagnostics());
            bridge.Start(4200);

            Assert.True(bridge.TryConnect(dialedPeer, out _));
            clientTransport.SetConnectionHealth(clientTransport.NextConnection, new RouteHealth(190, 0.5f, 10));
            hostTransport.SetRemoteSteamId(acceptedConnection, acceptedPeer);
            hostTransport.RaiseConnectionState(acceptedConnection, TunnelConnectionState.Connecting);
            hostTransport.RaiseConnectionState(acceptedConnection, TunnelConnectionState.Connected);
            hostTransport.SetConnectionHealth(acceptedConnection, new RouteHealth(60, 1f, 0));

            Assert.True(bridge.TryGetRouteHealth(dialedPeer, out RouteHealth dialed));
            Assert.Equal(190, dialed.RoundTripMilliseconds);
            Assert.True(bridge.TryGetRouteHealth(acceptedPeer, out RouteHealth accepted));
            Assert.Equal(60, accepted.RoundTripMilliseconds);
            Assert.False(bridge.TryGetRouteHealth(remoteSteamId: 99, out _));
        }
    }
}
