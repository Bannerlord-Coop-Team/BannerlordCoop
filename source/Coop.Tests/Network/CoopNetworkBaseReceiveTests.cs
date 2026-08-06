using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Tests.Extensions;
using LiteNetLib;
using Moq;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using Xunit;

namespace Coop.Tests.Network;

/// <summary>
/// Covers the shared receive path both endpoints use. It runs on the poll thread inside
/// <c>netManager.PollEvents()</c>, so an escaping exception abandons the rest of the tick for everyone.
/// </summary>
public sealed class CoopNetworkBaseReceiveTests : IDisposable
{
    private readonly Mock<ICommonSerializer> serializer = new Mock<ICommonSerializer>();
    private readonly Mock<IPacketManager> packetManager = new Mock<IPacketManager>();
    private readonly Mock<IMessagePacketHandler> messagePacketHandler = new Mock<IMessagePacketHandler>();
    private readonly CancellationTokenSource sessionCancellation = new CancellationTokenSource();
    private readonly TestNetwork network;
    private readonly NetPeer peer;

    public CoopNetworkBaseReceiveTests()
    {
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(60));
        config.SetupGet(value => value.NetworkPollInterval).Returns(TimeSpan.FromMilliseconds(25));
        config.SetupGet(value => value.UpdateTime).Returns(TimeSpan.FromMilliseconds(15));

        network = new TestNetwork(
            config.Object,
            serializer.Object,
            packetManager.Object,
            messagePacketHandler.Object,
            sessionCancellation);

        peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        peer.Setup(id: 1);
    }

    [Fact]
    public void HandleReceivedPayload_WhenAMessageHandlerThrows_DoesNotAbandonTheTick()
    {
        var message = Mock.Of<IMessage>();
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns(message);
        messagePacketHandler
            .Setup(h => h.PublishEvent(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
            .Throws(new InvalidOperationException("handler failure"));

        Assert.Null(Record.Exception(() => network.Receive(peer, new byte[] { 1, 2, 3 })));

        // Moq records the call before the throw, so this also pins that the message branch dispatched
        // at all — without it the test would pass even if the branch were dropped.
        messagePacketHandler.Verify(h => h.PublishEvent(peer, message), Times.Once);
    }

    [Fact]
    public void HandleReceivedPayload_WhenDeserializationThrows_DoesNotAbandonTheTick()
    {
        serializer
            .Setup(s => s.Deserialize(It.IsAny<byte[]>()))
            .Throws(new InvalidOperationException("malformed payload"));

        Assert.Null(Record.Exception(() => network.Receive(peer, new byte[] { 4, 5, 6 })));
    }

    [Fact]
    public void HandleReceivedPayload_WhenTeardownCancelsAHandler_LetsTheCancellationOut()
    {
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns(Mock.Of<IMessage>());
        messagePacketHandler
            .Setup(h => h.PublishEvent(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
            .Throws(new OperationCanceledException("the session ended before the marshal completed"));
        sessionCancellation.Cancel();

        Assert.IsType<OperationCanceledException>(
            Record.Exception(() => network.Receive(peer, new byte[] { 1 })));
    }

    [Fact]
    public void HandleReceivedPayload_WhenAHandlerIsCancelledWithoutTeardown_StillDoesNotAbandonTheTick()
    {
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns(Mock.Of<IMessage>());
        messagePacketHandler
            .Setup(h => h.PublishEvent(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
            .Throws(new OperationCanceledException("a handler's own cancellation"));

        // Only teardown gets the pass-through.
        Assert.Null(Record.Exception(() => network.Receive(peer, new byte[] { 2 })));
    }

    [Fact]
    public void HandleReceivedPayload_WithAnUnregisteredTypeId_IsRejectedWithoutDispatching()
    {
        // An unregistered type id deserializes to null, which is neither IPacket nor IMessage.
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns((object)null);

        Assert.Null(Record.Exception(() => network.Receive(peer, new byte[] { 7, 8, 9 })));

        packetManager.Verify(p => p.HandleReceive(It.IsAny<NetPeer>(), It.IsAny<IPacket>()), Times.Never);
        messagePacketHandler.Verify(h => h.PublishEvent(It.IsAny<NetPeer>(), It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void HandleReceivedPayload_WithAValidPacket_StillDispatchesIt()
    {
        var packet = Mock.Of<IPacket>();
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns(packet);

        network.Receive(peer, new byte[] { 10, 11, 12 });

        packetManager.Verify(p => p.HandleReceive(peer, packet), Times.Once);
    }

    [Fact]
    public void HandleReceivedPayload_WhenTheSameFaultRepeats_KeepsDispatchingLaterPayloads()
    {
        serializer
            .Setup(s => s.Deserialize(It.IsAny<byte[]>()))
            .Throws(new InvalidOperationException("malformed payload"));

        for (int attempt = 0; attempt < 5; attempt++)
        {
            network.Receive(peer, new byte[] { 13 });
        }

        // Throttling only silences the log; a good payload after a run of failures must still land.
        var packet = Mock.Of<IPacket>();
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns(packet);
        network.Receive(peer, new byte[] { 14 });

        packetManager.Verify(p => p.HandleReceive(peer, packet), Times.Once);
    }

    public void Dispose()
    {
        network.Dispose();
        sessionCancellation.Dispose();
    }

    /// <summary>
    /// Minimal concrete endpoint, mirroring the stub in <see cref="CoopNetworkBaseTests"/>, that exposes
    /// the protected dispatch so it can be driven without a live LiteNetLib reader.
    /// </summary>
    private sealed class TestNetwork : CoopNetworkBase
    {
        public TestNetwork(
            INetworkConfig config,
            ICommonSerializer serializer,
            IPacketManager packetManager,
            IMessagePacketHandler messagePacketHandler,
            CancellationTokenSource sessionCancellation)
            : base(config, serializer, packetManager, messagePacketHandler, sessionCancellation)
        {
        }

        public void Receive(NetPeer peer, byte[] payload) => HandleReceivedPayload(peer, payload);

        public override int Priority => 0;
        public override void Start() { }
        public override void SendAll(IPacket packet) { }
        public override void SendAllBut(NetPeer ignoredPeer, IPacket packet) { }
        public override void Update(TimeSpan frameTime) { }
        public override void OnPeerConnected(NetPeer peer) { }
        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }
        public override void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        public override void OnNetworkReceive(
            NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) { }

        public override void OnNetworkReceiveUnconnected(
            IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

        public override void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public override void OnConnectionRequest(ConnectionRequest request) { }
    }
}
