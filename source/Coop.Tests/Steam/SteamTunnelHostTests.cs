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
