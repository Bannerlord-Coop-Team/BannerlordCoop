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
            Assert.Contains(7u, transport.ClosedConnections);

            host.Start(4200);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connecting);
            Assert.Contains(7u, transport.AcceptedConnections);
        }

        [Fact]
        public void ConnectedPeer_AfterStop_IsClosedInsteadOfLeaked()
        {
            host.Start(4200);
            host.Stop();

            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            Assert.Equal(0, host.PeerCount);
            Assert.Contains(7u, transport.ClosedConnections);
        }

        [Fact]
        public void ConnectingPeer_IsClosedOnStopAndCannotAttachAfterRestart()
        {
            host.Start(4200);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connecting);

            host.Stop();
            host.Start(4201);
            transport.RaiseConnectionState(7, TunnelConnectionState.Connected);

            Assert.Equal(0, host.PeerCount);
            Assert.Contains(7u, transport.ClosedConnections);
        }

        [Fact]
        public void PeerLifecycle_TracksConnectedPeers()
        {
            host.Start(4200);

            RaiseConnectedPeer(7);
            Assert.Equal(1, host.PeerCount);

            RaiseConnectedPeer(8);
            Assert.Equal(2, host.PeerCount);

            transport.RaiseConnectionState(7, TunnelConnectionState.Closed);
            Assert.Equal(1, host.PeerCount);
        }

        [Fact]
        public void ServerVisibleRelayEndpoint_ResolvesAuthenticatedRemoteSteamId()
        {
            using var serverSocket = CreateServerSocket();
            const ulong remoteSteamId = 76561198000000042;

            var relayEndpoint = ConnectPeerAndObserveServerEndpoint(serverSocket, 7, remoteSteamId);

            Assert.True(host.TryGetRemoteSteamId(relayEndpoint, out var actualSteamId));
            Assert.Equal(remoteSteamId, actualSteamId);

            int unrelatedPort = relayEndpoint.Port == 1 ? 2 : 1;
            Assert.False(host.TryGetRemoteSteamId(
                new IPEndPoint(IPAddress.Loopback, unrelatedPort), out _));
        }

        [Fact]
        public void ClosedPeer_RemovesRelayEndpointIdentity()
        {
            using var serverSocket = CreateServerSocket();
            var relayEndpoint = ConnectPeerAndObserveServerEndpoint(
                serverSocket, 7, 76561198000000042);

            transport.RaiseConnectionState(7, TunnelConnectionState.Closed);

            Assert.False(host.TryGetRemoteSteamId(relayEndpoint, out _));
        }

        [Fact]
        public void Stop_RemovesEveryRelayEndpointIdentity()
        {
            using var serverSocket = CreateServerSocket();
            var relayEndpoint = ConnectPeerAndObserveServerEndpoint(
                serverSocket, 7, 76561198000000042);

            host.Stop();

            Assert.False(host.TryGetRemoteSteamId(relayEndpoint, out _));
        }

        [Fact]
        public void Stop_ClosesEveryPeer()
        {
            host.Start(4200);
            RaiseConnectedPeer(7);

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
            RaiseConnectedPeer(7);

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
            RaiseConnectedPeer(7);

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
            RaiseConnectedPeer(7);

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
        public void TwoPeers_GetDistinctRelayPorts()
        {
            using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            serverSocket.ReceiveTimeout = 5000;
            int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;

            host.Start(serverPort);
            RaiseConnectedPeer(7);
            RaiseConnectedPeer(8);

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

        private static Socket CreateServerSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            socket.ReceiveTimeout = 5000;
            return socket;
        }

        private void RaiseConnectedPeer(uint connection)
        {
            transport.RaiseConnectionState(connection, TunnelConnectionState.Connecting);
            transport.RaiseConnectionState(connection, TunnelConnectionState.Connected);
        }

        private IPEndPoint ConnectPeerAndObserveServerEndpoint(
            Socket serverSocket, uint connection, ulong remoteSteamId)
        {
            int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;
            transport.SetRemoteSteamId(connection, remoteSteamId);
            host.Start(serverPort);
            RaiseConnectedPeer(connection);

            transport.EnqueueReceive(connection, new byte[] { 0 });
            var buffer = new byte[SteamTunnel.MaxDatagramBytes];
            EndPoint relayEndpoint = new IPEndPoint(IPAddress.Any, 0);
            serverSocket.ReceiveFrom(buffer, ref relayEndpoint);
            return (IPEndPoint)relayEndpoint;
        }
    }
}
