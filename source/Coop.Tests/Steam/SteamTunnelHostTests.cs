using Coop.Steam;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamTunnelHostTests : IDisposable
    {
        private readonly FakeSteamTunnelTransport transport = new FakeSteamTunnelTransport();
        private readonly SteamTunnelHost host;

        public SteamTunnelHostTests()
        {
            host = new SteamTunnelHost(transport);
        }

        public void Dispose() => host.Dispose();

        [Fact]
        public void Start_ListensOnce()
        {
            host.Start(4200);
            host.Start(4200);

            Assert.True(host.IsListening);
            Assert.True(transport.Listening);
            Assert.Equal(1, transport.RelayAccessCalls);
        }

        [Fact]
        public void ConnectingPeer_IsAcceptedOnlyWhileListening()
        {
            transport.RaiseConnectionState(7, TunnelConnectionState.Connecting);
            Assert.Empty(transport.AcceptedConnections);

            host.Start(4200);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connecting);
            Assert.Contains(7u, transport.AcceptedConnections);
        }

        [Fact]
        public void PeerLifecycle_TracksConnectedPeers()
        {
            host.Start(4200);

            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);
            Assert.Equal(1, host.PeerCount);

            transport.RaiseConnectionState(8, TunnelConnectionState.Connected);
            Assert.Equal(2, host.PeerCount);

            transport.RaiseConnectionState(7, TunnelConnectionState.Closed);
            Assert.Equal(1, host.PeerCount);
        }

        [Fact]
        public void Stop_ClosesEveryPeer()
        {
            host.Start(4200);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            host.Stop();

            Assert.False(host.IsListening);
            Assert.False(transport.Listening);
            Assert.Equal(0, host.PeerCount);
            Assert.Contains(7u, transport.ClosedConnections);
        }

        [Fact]
        public void Datagrams_FlowBothWaysBetweenPeerAndServer()
        {
            using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            serverSocket.ReceiveTimeout = 5000;
            int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;

            host.Start(serverPort);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            var fromPeer = new byte[] { 1, 2, 3 };
            transport.EnqueueReceive(7, fromPeer);

            var buffer = new byte[SteamTunnel.MaxDatagramBytes];
            EndPoint peerRelayEndpoint = new IPEndPoint(IPAddress.Any, 0);
            int received = serverSocket.ReceiveFrom(buffer, ref peerRelayEndpoint);
            Assert.Equal(fromPeer, buffer.Take(received).ToArray());

            var reply = new byte[] { 4, 5 };
            serverSocket.SendTo(reply, peerRelayEndpoint);
            SteamTunnelClientTests.WaitUntil(() => transport.SentDatagrams.Length == 1);
            Assert.Equal(reply, transport.SentDatagrams[0].Data);
            Assert.Equal(7u, transport.SentDatagrams[0].Connection);
        }

        [Fact]
        public void BackpressuredSend_IsRetriedInOrderWithoutLoss()
        {
            using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            serverSocket.ReceiveTimeout = 5000;
            int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;

            host.Start(serverPort);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            // Learn the peer's relay endpoint from one steam->server datagram.
            transport.EnqueueReceive(7, new byte[] { 0 });
            var buffer = new byte[SteamTunnel.MaxDatagramBytes];
            EndPoint peerRelayEndpoint = new IPEndPoint(IPAddress.Any, 0);
            serverSocket.ReceiveFrom(buffer, ref peerRelayEndpoint);

            transport.FailSendsRemaining = 2;
            serverSocket.SendTo(new byte[] { 1 }, peerRelayEndpoint);
            SteamTunnelClientTests.WaitUntil(() => transport.RejectedSends >= 1);
            serverSocket.SendTo(new byte[] { 2 }, peerRelayEndpoint);

            SteamTunnelClientTests.WaitUntil(() => transport.SentDatagrams.Length == 2);
            Assert.Equal(new byte[] { 1 }, transport.SentDatagrams[0].Data);
            Assert.Equal(new byte[] { 2 }, transport.SentDatagrams[1].Data);
        }

        [Fact]
        public void DroppableDatagram_IsDroppedNotParkedUnderBackpressure()
        {
            using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            serverSocket.ReceiveTimeout = 5000;
            int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;

            host.Start(serverPort);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            transport.EnqueueReceive(7, new byte[] { 0 });
            var buffer = new byte[SteamTunnel.MaxDatagramBytes];
            EndPoint peerRelayEndpoint = new IPEndPoint(IPAddress.Any, 0);
            serverSocket.ReceiveFrom(buffer, ref peerRelayEndpoint);

            transport.FailSendsRemaining = 1;
            // First byte 0 = LiteNetLib Unreliable: refused under pressure means dropped.
            serverSocket.SendTo(new byte[] { 0, 9 }, peerRelayEndpoint);
            SteamTunnelClientTests.WaitUntil(() => transport.RejectedSends >= 1);

            // First byte 1 = Channeled (reliable class): must still get through.
            serverSocket.SendTo(new byte[] { 1, 7 }, peerRelayEndpoint);
            SteamTunnelClientTests.WaitUntil(() => transport.SentDatagrams.Length == 1);
            Assert.Equal(new byte[] { 1, 7 }, transport.SentDatagrams[0].Data);
        }

        [Fact]
        public void Governor_RaisesFloorOnlyWhileBacklogWaits()
        {
            host.Start(4200);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            transport.PendingReliableBytes = 1024 * 1024;
            SteamTunnelClientTests.WaitUntil(() =>
                transport.LastFloorFor(7) == SteamTunnel.TransferFloorBytesPerSecond);

            transport.PendingReliableBytes = 0;
            SteamTunnelClientTests.WaitUntil(() => transport.LastFloorFor(7) == 256 * 1024);
        }

        [Fact]
        public void Governor_BacksOffWhenDeliveryQualitySags()
        {
            host.Start(4200);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            transport.PendingReliableBytes = 1024 * 1024;
            SteamTunnelClientTests.WaitUntil(() =>
                transport.LastFloorFor(7) == SteamTunnel.TransferFloorBytesPerSecond);

            transport.Quality = 0.5f;
            SteamTunnelClientTests.WaitUntil(() =>
                transport.LastFloorFor(7) == SteamTunnel.TransferFloorBytesPerSecond / 2);
        }

        [Fact]
        public void TwoPeers_GetDistinctRelayPorts()
        {
            using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            serverSocket.ReceiveTimeout = 5000;
            int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;

            host.Start(serverPort);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);
            transport.RaiseConnectionState(8, TunnelConnectionState.Connected);

            transport.EnqueueReceive(7, new byte[] { 7 });
            transport.EnqueueReceive(8, new byte[] { 8 });

            var buffer = new byte[SteamTunnel.MaxDatagramBytes];
            EndPoint firstSender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint secondSender = new IPEndPoint(IPAddress.Any, 0);
            serverSocket.ReceiveFrom(buffer, ref firstSender);
            serverSocket.ReceiveFrom(buffer, ref secondSender);

            // LiteNetLib keys peers by endpoint, so each tunneled client must arrive
            // from its own local port.
            Assert.NotEqual(((IPEndPoint)firstSender).Port, ((IPEndPoint)secondSender).Port);
        }
    }
}
