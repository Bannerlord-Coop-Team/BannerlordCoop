using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Connection.Handlers;
using Coop.Core.Common;
using Coop.Core.Server.Connections;
using GameInterface.Services.GameState.Interfaces;
using LiteNetLib;
using Moq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server.Connections;

/// <summary>Verifies that server join failures reach the client disconnect popup over the transport.</summary>
public class JoinPeerTerminatorTests
{
    [Theory]
    [InlineData(
        "JoinReplayAppliedTimeout",
        "Joining the campaign timed out while synchronizing.\n" +
        "The server stopped this join to keep the campaign responsive. Please try again.")]
    [InlineData(
        "JoinReplayQueueLimit",
        "Joining the campaign stopped because its synchronization queue exceeded the safety limit.\n" +
        "Please try again.")]
    [InlineData(
        "JoinCampaignEntryTimeout",
        "Joining the campaign timed out while loading the transferred save.\n" +
        "The server stopped this join to keep the campaign responsive. Please try again.")]
    public void Disconnect_DeliversReasonThroughClientBeforeFinalizing(
        string code,
        string expectedMessage)
    {
        RunDisconnect(peer => new JoinPeerTerminator().Disconnect(peer, code), code, expectedMessage);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 20 })]
    [InlineData(new byte[] { 20, 0, 1 })]
    public void Disconnect_MissingOrMalformedReasonUsesGenericPopup(byte[] data)
    {
        RunDisconnect(peer => peer.Disconnect(data), null, "You have been Disconnected");
    }

    [Fact]
    public void Disconnect_UnknownReasonUsesGenericPopup()
    {
        RunDisconnect(peer => new JoinPeerTerminator().Disconnect(peer, "UnknownReason"),
            "UnknownReason", "You have been Disconnected");
    }

    private static void RunDisconnect(Action<NetPeer> disconnect, string? expectedReason, string expectedMessage)
    {
        using var broker = new TestMessageBroker();
        var gameState = new Mock<IGameStateInterface>(MockBehavior.Strict);
        var finalizer = new Mock<ICoopFinalizer>(MockBehavior.Strict);
        var sequence = new MockSequence();
        gameState.InSequence(sequence).Setup(value => value.GoToMainMenu());
        finalizer.InSequence(sequence).Setup(value => value.Finalize(expectedMessage));
        using var handler = new DisconnectHandler(broker, finalizer.Object, gameState.Object);

        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(5));
        config.SetupGet(value => value.UpdateTime).Returns(TimeSpan.FromMilliseconds(15));
        config.SetupGet(value => value.NetworkPollInterval).Returns(TimeSpan.FromMilliseconds(25));
        using var cancellation = new CancellationTokenSource();
        using var client = new CoopClient(config.Object, broker, Mock.Of<IPacketManager>(),
            Mock.Of<IMessagePacketHandler>(), Mock.Of<ICommonSerializer>(),
            Mock.Of<IReliableMessageBatcher<NetPeer>>(), cancellation);
        var serverListener = new EventBasedNetListener();
        serverListener.ConnectionRequestEvent += request => request.AcceptIfKey("join-disconnect-test");
        NetPeer? serverPeer = null;
        serverListener.PeerConnectedEvent += peer => serverPeer = peer;
        var serverTransport = new NetManager(serverListener);
        var clientTransport = new NetManager(client);

        try
        {
            Assert.True(serverTransport.StartInManualMode(0));
            Assert.True(clientTransport.StartInManualMode(0));
            clientTransport.Connect(IPAddress.Loopback.ToString(), serverTransport.LocalPort, "join-disconnect-test");
            PumpUntil(serverTransport, clientTransport, () =>
                serverPeer != null && broker.GetMessagesFromType<NetworkConnected>().Any());

            disconnect(serverPeer!);

            PumpUntil(serverTransport, clientTransport, () =>
                broker.GetMessagesFromType<NetworkDisconnected>().Any());
            var disconnected = Assert.Single(broker.GetMessagesFromType<NetworkDisconnected>());
            Assert.Equal(DisconnectReason.RemoteConnectionClose, disconnected.DisconnectInfo.Reason);
            Assert.Equal(expectedReason, disconnected.ServerReason);
            Assert.True(disconnected.DisconnectInfo.AdditionalData.IsNull);
            gameState.Verify(value => value.GoToMainMenu(), Times.Once);
            finalizer.Verify(value => value.Finalize(expectedMessage), Times.Once);
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

        Assert.True(complete(), "The bounded loopback join disconnect did not finish.");
    }
}
