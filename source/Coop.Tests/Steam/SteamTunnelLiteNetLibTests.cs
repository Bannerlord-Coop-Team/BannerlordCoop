using Coop.Steam;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamTunnelLiteNetLibTests : IDisposable
    {
        private const string ConnectionKey = "tunnel-lane-test";

        private readonly EventBasedNetListener serverListener = new EventBasedNetListener();
        private readonly EventBasedNetListener clientListener = new EventBasedNetListener();
        private readonly NetManager server;
        private readonly NetManager client;
        private readonly List<byte[]> reliableReceived = new List<byte[]>();
        private readonly List<byte[]> unreliableReceived = new List<byte[]>();
        private SplittingRelay relay;

        public SteamTunnelLiteNetLibTests()
        {
            serverListener.ConnectionRequestEvent += request => request.AcceptIfKey(ConnectionKey);
            serverListener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                var target = method == DeliveryMethod.Unreliable ? unreliableReceived : reliableReceived;
                target.Add(reader.GetRemainingBytes());
                reader.Recycle();
            };
            server = new NetManager(serverListener);
            client = new NetManager(clientListener);
        }

        public void Dispose()
        {
            // Relay first, otherwise it forwards to the stopped server and the ICMP refusal throws on the relay thread, which kills the test host
            relay?.Dispose();
            client.Stop();
            server.Stop();
        }

        [Fact]
        public void SplitMergedDatagrams_AreDeliveredByLiteNetLib()
        {
            Assert.True(server.Start(IPAddress.Loopback, IPAddress.IPv6Loopback, 0));
            relay = new SplittingRelay(server.LocalPort);
            Assert.True(client.Start());
            NetPeer peer = client.Connect("127.0.0.1", relay.Port, ConnectionKey);
            PollUntil(() => peer.ConnectionState == ConnectionState.Connected && server.ConnectedPeersCount == 1);

            // Queued in one burst, so one peer update merges reliable and unreliable packets together.
            const int burst = 40;
            for (byte i = 0; i < burst; i++)
            {
                peer.Send(new byte[] { i, 1 }, DeliveryMethod.ReliableOrdered);
                peer.Send(new byte[] { i, 2 }, DeliveryMethod.Unreliable);
            }
            PollUntil(() => reliableReceived.Count == burst && unreliableReceived.Count == burst);

            // One packet per lane, so each lane gets a single-packet Merged datagram.
            peer.Send(new byte[] { burst, 1 }, DeliveryMethod.ReliableOrdered);
            peer.Send(new byte[] { burst, 2 }, DeliveryMethod.Unreliable);
            PollUntil(() => reliableReceived.Count == burst + 1 && unreliableReceived.Count == burst + 1);

            Assert.True(relay.SplitDatagrams > 0, "No Merged datagram was split across both lanes");
            Assert.Equal(
                Enumerable.Range(0, burst + 1).Select(i => new byte[] { (byte)i, 1 }),
                reliableReceived);
            Assert.Equal(
                Enumerable.Range(0, burst + 1).Select(i => new byte[] { (byte)i, 2 }),
                unreliableReceived.OrderBy(data => data[0]));
        }

        // Pumps both managers' events until the condition holds, failing after 10 seconds.
        private void PollUntil(Func<bool> condition)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                Assert.True(stopwatch.ElapsedMilliseconds < 10000, "LiteNetLib did not deliver within 10s");
                client.PollEvents();
                server.PollEvents();
                Thread.Sleep(5);
            }
        }

        /// <summary>
        /// Loopback UDP relay between a LiteNetLib client and server that forwards each datagram as its
        /// per-lane parts, the way the tunnel pumps hand them to Steam.
        /// </summary>
        private sealed class SplittingRelay : IDisposable
        {
            private readonly Socket clientSide;
            private readonly Socket serverSide;
            private readonly Thread thread;
            private volatile bool running = true;
            private int splitDatagrams;

            public SplittingRelay(int serverPort)
            {
                clientSide = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                clientSide.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                serverSide = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverSide.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                serverSide.Connect(new IPEndPoint(IPAddress.Loopback, serverPort));
                thread = new Thread(Run) { IsBackground = true };
                thread.Start();
            }

            public int Port => ((IPEndPoint)clientSide.LocalEndPoint!).Port;

            public int SplitDatagrams => Volatile.Read(ref splitDatagrams);

            // Forwards client datagrams to the server and replies back, splitting both directions.
            private void Run()
            {
                var buffer = new byte[SteamTunnel.MaxDatagramBytes];
                var droppable = new byte[SteamTunnel.MaxDatagramBytes];
                var reliable = new byte[SteamTunnel.MaxDatagramBytes];
                EndPoint clientEndpoint = null;
                while (running)
                {
                    if (clientSide.Available > 0)
                    {
                        EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        int length = clientSide.ReceiveFrom(buffer, ref sender);
                        clientEndpoint = sender;
                        Forward(buffer, length, droppable, reliable, (data, size) => serverSide.Send(data, size, SocketFlags.None));
                    }

                    if (serverSide.Available > 0 && clientEndpoint != null)
                    {
                        int length = serverSide.Receive(buffer);
                        EndPoint target = clientEndpoint;
                        Forward(buffer, length, droppable, reliable, (data, size) => clientSide.SendTo(data, size, SocketFlags.None, target));
                    }

                    Thread.Sleep(1);
                }
            }

            // Sends the droppable part, then the reliable part, counting datagrams split across both.
            private void Forward(byte[] buffer, int length, byte[] droppable, byte[] reliable, Action<byte[], int> send)
            {
                SteamTunnel.SplitByLane(buffer, length, droppable, out int droppableLength, reliable, out int reliableLength);
                if (droppableLength > 0) send(droppable, droppableLength);
                if (reliableLength > 0) send(reliable, reliableLength);
                if (droppableLength > 0 && reliableLength > 0) Interlocked.Increment(ref splitDatagrams);
            }

            public void Dispose()
            {
                running = false;
                thread.Join();
                clientSide.Close();
                serverSide.Close();
            }
        }
    }
}
