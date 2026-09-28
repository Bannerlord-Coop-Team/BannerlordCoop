using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Shutdown;
using Coop.Core.Server.Services.Time;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Handlers;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using LiteNetLib.Utils;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server.Services.Shutdown;

/// <summary>Drives <see cref="CoopServer.OnConnectionRequest"/> and the client reject popup over loopback.</summary>
public class ServerAdmissionGateTests
{
    private const string Password = "join-gate-test";
    private const string RestartingPopup = "The server is restarting. Try again in a few minutes.";
    private const string PasswordPopup = "The server password is incorrect.";

    [Fact]
    public void Gate_StartsOpenAndTogglesBothWays()
    {
        var gate = new ServerAdmissionGate();
        Assert.True(gate.IsOpen);

        gate.Close();
        Assert.False(gate.IsOpen);

        gate.Open();
        Assert.True(gate.IsOpen);
    }

    [Fact]
    public void ClosedGate_CorrectPassword_ShowsTheRestartingPopup()
    {
        Assert.Equal(RestartingPopup, ConnectAndReadPopup(gateOpen: false, Password));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongPassword_IsRefusedForThePasswordWhetherOrNotTheGateIsClosed(bool gateOpen)
    {
        Assert.Equal(PasswordPopup, ConnectAndReadPopup(gateOpen, "wrong"));
    }

    [Fact]
    public void OpenGate_CorrectPassword_Connects()
    {
        using var broker = new TestMessageBroker();
        RunLoopback(broker, gateOpen: true, Password,
            () => broker.GetMessagesFromType<NetworkConnected>().Any());

        Assert.Empty(broker.GetMessagesFromType<SendPopupMessage>());
    }

    [Theory]
    [InlineData(false, Password, RestartingPopup)]
    [InlineData(true, "wrong", PasswordPopup)]
    public void RejectPopup_OutlivesTheTeardownThatEndsTheSession(bool gateOpen, string suppliedPassword, string expected)
    {
        using var broker = new TestMessageBroker();
        var shown = new ConcurrentQueue<string>();
        using var popups = new DebugMessageHandler(broker, shown.Enqueue);
        using var session = new CancellationTokenSource();
        // The EndCoopMode teardown runs inline on the game thread and cancels the session.
        Action<MessagePayload<EndCoopMode>> tearDown = _ => session.Cancel();
        broker.Subscribe(tearDown);

        RunLoopback(broker, gateOpen, suppliedPassword, () => !shown.IsEmpty, session);

        Assert.Equal(expected, Assert.Single(shown));
        GC.KeepAlive(tearDown);
    }

    [Fact]
    public void UnknownRejectCode_ShowsTheGenericPopup()
    {
        using var broker = new TestMessageBroker();
        RunLoopback(broker, request =>
        {
            var reason = new NetDataWriter();
            reason.Put((byte)200);
            request.Reject(reason);
        }, Password, () => broker.GetMessagesFromType<SendPopupMessage>().Any());

        Assert.Equal("The server rejected the connection.", Assert.Single(broker.GetMessagesFromType<SendPopupMessage>()).Text);
    }

    private static string ConnectAndReadPopup(bool gateOpen, string suppliedPassword)
    {
        using var broker = new TestMessageBroker();
        RunLoopback(broker, gateOpen, suppliedPassword, () => broker.GetMessagesFromType<SendPopupMessage>().Any());

        Assert.Empty(broker.GetMessagesFromType<NetworkConnected>());
        return Assert.Single(broker.GetMessagesFromType<SendPopupMessage>()).Text;
    }

    private static void RunLoopback(
        TestMessageBroker broker,
        bool gateOpen,
        string suppliedPassword,
        Func<bool> complete,
        CancellationTokenSource? session = null)
    {
        var gate = new ServerAdmissionGate();
        if (!gateOpen) gate.Close();

        using var cancellation = new CancellationTokenSource();
        using var server = new CoopServer(
            CreateConfig(Password).Object,
            new TestMessageBroker(),
            Mock.Of<IPacketManager>(),
            Mock.Of<IMessagePacketHandler>(),
            Mock.Of<IConnectionMessageQueue>(),
            gate,
            Mock.Of<IControllerIdProvider>(),
            Mock.Of<IMissionManager>(),
            new Lazy<IOverloadedPeerManager>(() => Mock.Of<IOverloadedPeerManager>()),
            Mock.Of<ISendCoalescer>(),
            Mock.Of<ICommonSerializer>(),
            Mock.Of<IReliableMessageBatcher<NetPeer>>(),
            cancellation);

        RunLoopback(broker, server.OnConnectionRequest, suppliedPassword, complete, session);
    }

    private static void RunLoopback(
        TestMessageBroker broker,
        Action<ConnectionRequest> onConnectionRequest,
        string suppliedPassword,
        Func<bool> complete,
        CancellationTokenSource? session = null)
    {
        using var ownSession = new CancellationTokenSource();
        var cancellation = session ?? ownSession;
        using var client = new CoopClient(CreateConfig(null).Object, broker, Mock.Of<IPacketManager>(),
            Mock.Of<IMessagePacketHandler>(), Mock.Of<ICommonSerializer>(),
            Mock.Of<IReliableMessageBatcher<NetPeer>>(), cancellation);
        var serverListener = new EventBasedNetListener();
        serverListener.ConnectionRequestEvent += request => onConnectionRequest(request);
        var serverTransport = new NetManager(serverListener);
        var clientTransport = new NetManager(client);

        try
        {
            Assert.True(serverTransport.StartInManualMode(0));
            Assert.True(clientTransport.StartInManualMode(0));
            clientTransport.Connect(IPAddress.Loopback.ToString(), serverTransport.LocalPort, suppliedPassword);

            PumpUntil(serverTransport, clientTransport, cancellation.Token, complete);
        }
        finally
        {
            clientTransport.Stop();
            serverTransport.Stop();
        }
    }

    private static Mock<INetworkConfig> CreateConfig(string? token)
    {
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.Token).Returns(token!);
        config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(5));
        config.SetupGet(value => value.UpdateTime).Returns(TimeSpan.FromMilliseconds(15));
        config.SetupGet(value => value.NetworkPollInterval).Returns(TimeSpan.FromMilliseconds(25));
        return config;
    }

    private static void PumpUntil(NetManager server, NetManager client, CancellationToken session, Func<bool> complete)
    {
        // The client publishes the reject popup through the test game-loop pump.
        var timer = Stopwatch.StartNew();
        while (!complete() && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            server.ManualUpdate(15);
            client.ManualUpdate(15);
            server.PollEvents();
            // Polled inside the session like CoopNetworkBase's poller.
            using (GameThread.ActivateCancellation(session))
            {
                client.PollEvents();
            }
            Thread.Sleep(1);
        }

        Assert.True(complete(), "The bounded loopback connection did not finish.");
    }
}
