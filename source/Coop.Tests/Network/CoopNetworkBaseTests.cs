using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using LiteNetLib;
using Moq;
using System;
using System.Net;
using System.Net.Sockets;
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

        using var network = new TestNetwork(config.Object, Mock.Of<ICommonSerializer>());

        Assert.Equal(60_000, network.AppliedDisconnectTimeout);
    }

    private sealed class TestNetwork : CoopNetworkBase
    {
        public TestNetwork(INetworkConfig config, ICommonSerializer serializer)
            : base(config, serializer)
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
