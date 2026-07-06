using Coop.Steam;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamTunnelClientTests : IDisposable
    {
        private readonly FakeSteamTunnelTransport transport = new FakeSteamTunnelTransport();
        private readonly SteamTunnelClient client;

        public SteamTunnelClientTests()
        {
            client = new SteamTunnelClient(transport);
        }

        public void Dispose() => client.Dispose();

        // The pump drains on a background poller, so data-path assertions wait for it.
        internal static void WaitUntil(Func<bool> condition)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Pump did not forward within 5s");
                Thread.Sleep(10);
            }
        }

        [Fact]
        public void Start_BindsLoopbackPortAndConnectsToHost()
        {
            client.Start(76561198000000042);

            Assert.NotEqual(0, client.LocalPort);
            Assert.Equal(76561198000000042ul, transport.ConnectedHost);
            Assert.Equal(1, transport.RelayAccessCalls);
        }

        [Fact]
        public void Datagrams_FlowBothWaysThroughThePump()
        {
            client.Start(76561198000000042);

            using var liteNetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            liteNetSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var pumpEndpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);

            var outbound = new byte[] { 1, 2, 3, 4 };
            liteNetSocket.SendTo(outbound, pumpEndpoint);
            WaitUntil(() => transport.SentDatagrams.Length == 1);
            Assert.Equal(outbound, transport.SentDatagrams[0].Data);
            Assert.Equal(transport.NextConnection, transport.SentDatagrams[0].Connection);

            var inbound = new byte[] { 9, 8, 7 };
            transport.EnqueueReceive(transport.NextConnection, inbound);
            liteNetSocket.ReceiveTimeout = 5000;
            var buffer = new byte[SteamTunnel.MaxDatagramBytes];
            int received = liteNetSocket.Receive(buffer);
            Assert.Equal(inbound, buffer.Take(received).ToArray());
        }

        [Fact]
        public void BackpressuredSend_IsRetriedInOrderWithoutLoss()
        {
            client.Start(76561198000000042);

            using var liteNetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            liteNetSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var pumpEndpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);

            transport.FailSendsRemaining = 2;
            liteNetSocket.SendTo(new byte[] { 1 }, pumpEndpoint);
            WaitUntil(() => transport.RejectedSends >= 1);
            liteNetSocket.SendTo(new byte[] { 2 }, pumpEndpoint);

            WaitUntil(() => transport.SentDatagrams.Length == 2);
            Assert.Equal(new byte[] { 1 }, transport.SentDatagrams[0].Data);
            Assert.Equal(new byte[] { 2 }, transport.SentDatagrams[1].Data);
        }

        [Fact]
        public void DroppableDatagram_IsDroppedNotParkedUnderBackpressure()
        {
            client.Start(76561198000000042);

            using var liteNetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            liteNetSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var pumpEndpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);

            transport.FailSendsRemaining = 1;
            // First byte 0 = LiteNetLib Unreliable: refused under pressure means dropped.
            liteNetSocket.SendTo(new byte[] { 0, 9 }, pumpEndpoint);
            WaitUntil(() => transport.RejectedSends >= 1);

            // First byte 1 = Channeled (reliable class): must still get through.
            liteNetSocket.SendTo(new byte[] { 1, 7 }, pumpEndpoint);
            WaitUntil(() => transport.SentDatagrams.Length == 1);
            Assert.Equal(new byte[] { 1, 7 }, transport.SentDatagrams[0].Data);
        }

        [Fact]
        public void InboundBeforeAnyOutbound_IsDroppedNotMisdelivered()
        {
            client.Start(76561198000000042);

            transport.EnqueueReceive(transport.NextConnection, new byte[] { 5 });

            // Drained without a destination: the pump has not learned the client endpoint yet.
            WaitUntil(() => transport.ReceiveDatagram(transport.NextConnection, new byte[16]) == 0);
        }

        [Fact]
        public void Dispose_ClosesConnectionAndTransport()
        {
            client.Start(76561198000000042);

            client.Dispose();

            Assert.Contains(transport.NextConnection, transport.ClosedConnections);
            Assert.True(transport.Disposed);
        }
    }
}
