using Common.BattleBenchmark;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Tests.Extensions;
using Coop.Core.Common.Network;
using LiteNetLib;
using Moq;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace Coop.Tests.Network;

public class CoopNetworkBaseTests
{
    [Fact]
    public void Constructor_UsesEstablishedDisconnectTimeout()
    {
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.ConnectionTimeout).Returns(TimeSpan.FromSeconds(10));
        config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(60));
        config.SetupGet(value => value.NetworkPollInterval).Returns(TimeSpan.FromMilliseconds(25));
        config.SetupGet(value => value.UpdateTime).Returns(TimeSpan.FromMilliseconds(15));

        ICommonSerializer serializer = Mock.Of<ICommonSerializer>();
        var reliableMessageBatcher = new ReliableMessageBatcher<NetPeer>(serializer);
        using var sessionCancellation = new CancellationTokenSource();
        using var network = new TestNetwork(
            config.Object,
            serializer,
            reliableMessageBatcher,
            sessionCancellation);

        Assert.Equal(60_000, network.AppliedDisconnectTimeout);
    }

    [Fact]
    public void DiscardPendingMessages_RemovesOnlyTheAbortedPeersBufferedPayload()
    {
        var config = Mock.Of<INetworkConfig>();
        var serializer = Mock.Of<ICommonSerializer>();
        var batcher = new ReliableMessageBatcher<NetPeer>(serializer);
        using var cancellation = new CancellationTokenSource();
        using var network = new TestNetwork(config, serializer, batcher, cancellation);
        var peers = new Mocks.TestNetwork();
        var aborted = peers.CreatePeer();
        var live = peers.CreatePeer();
        live.Setup(live.Id, "127.0.0.2");
        var sent = new System.Collections.Generic.List<NetPeer>();
        batcher.Send(aborted, new byte[] { 1 }, 1020, (peer, _) => sent.Add(peer));
        batcher.Send(live, new byte[] { 2 }, 1020, (peer, _) => sent.Add(peer));
        Assert.Empty(sent);
        ((IBufferedNetwork)network).DiscardPendingMessages(aborted);
        batcher.FlushAll(_ => true, (peer, _) => sent.Add(peer));
        Assert.Same(live, Assert.Single(sent));
    }

    [Fact]
    public void Send_RecordingCountsARelayFromTheBufferItSends()
    {
        var plain = new ProtoBufSerializer(new SerializableTypeMapper());
        var serializer = new Mock<ICommonSerializer>();
        serializer.Setup(value => value.Serialize(It.IsAny<object>())).Returns<object>(plain.Serialize);
        var recorder = new Mock<IBattleBenchmarkRecorder>();
        recorder.SetupGet(value => value.IsRecording).Returns(true);
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        using var network = new TestNetwork(config.Object, serializer.Object,
            new ReliableMessageBatcher<NetPeer>(plain), cancellation, recorder.Object);

        var serverListener = new EventBasedNetListener();
        serverListener.ConnectionRequestEvent += request => request.AcceptIfKey("relay-benchmark-test");
        byte[]? received = null;
        serverListener.NetworkReceiveEvent += (_, reader, _, _) =>
        {
            received = reader.GetRemainingBytes();
            reader.Recycle();
        };
        var serverTransport = new NetManager(serverListener);
        var clientTransport = new NetManager(new EventBasedNetListener());

        try
        {
            Assert.True(serverTransport.StartInManualMode(IPAddress.Loopback, IPAddress.IPv6Loopback, 0));
            Assert.True(clientTransport.StartInManualMode(IPAddress.Loopback, IPAddress.IPv6Loopback, 0));
            NetPeer server = clientTransport.Connect(IPAddress.Loopback.ToString(), serverTransport.LocalPort, "relay-benchmark-test");
            PumpUntil(serverTransport, clientTransport, () => server.ConnectionState == ConnectionState.Connected);

            network.Send(server, new RelayPacket(DeliveryMethod.ReliableOrdered, "MapEvent_Created_0000", "76561198000000042", new byte[300]));
            PumpUntil(serverTransport, clientTransport, () => received != null);

            serializer.Verify(value => value.Serialize(It.IsAny<object>()), Times.Once);
            recorder.Verify(value => value.RecordSent("76561198000000042", true, received!.Length,
                server.GetMaxSinglePacketSize(DeliveryMethod.ReliableOrdered), true), Times.Once);
        }
        finally
        {
            clientTransport.Stop();
            serverTransport.Stop();
        }
    }

    private static void PumpUntil(NetManager server, NetManager client, Func<bool> complete)
    {
        var timer = Stopwatch.StartNew();
        while (!complete() && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            server.ManualUpdate(15);
            client.ManualUpdate(15);
            server.PollEvents();
            client.PollEvents();
            Thread.Sleep(1);
        }

        Assert.True(complete(), "The bounded loopback relay send did not finish.");
    }

    private sealed class TestNetwork : CoopNetworkBase
    {
        public TestNetwork(
            INetworkConfig config,
            ICommonSerializer serializer,
            IReliableMessageBatcher<NetPeer> reliableMessageBatcher,
            CancellationTokenSource sessionCancellation,
            IBattleBenchmarkRecorder? battleBenchmark = null)
            : base(config, serializer, reliableMessageBatcher, sessionCancellation, battleBenchmark)
        {
        }

        public int AppliedDisconnectTimeout => netManager.DisconnectTimeout;

        public override int Priority => 0;

        public override void Start()
        {
        }

        public override void SendAll(IPacket packet)
        {
        }

        public override void SendAllBut(NetPeer ignoredPeer, IPacket packet)
        {
        }

        public override void Update(TimeSpan frameTime)
        {
        }

        public override void OnPeerConnected(NetPeer peer)
        {
        }

        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
        }

        public override void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
        }

        public override void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            byte channelNumber,
            DeliveryMethod deliveryMethod)
        {
        }

        public override void OnNetworkReceiveUnconnected(
            IPEndPoint remoteEndPoint,
            NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
        }

        public override void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public override void OnConnectionRequest(ConnectionRequest request)
        {
        }
    }
}
