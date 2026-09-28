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
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Kingdoms;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.Save.Messages;
using Coop.Core.Server.Services.Shutdown;
using Coop.Core.Server.Services.Time;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Interfaces;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Handlers;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Save.Messages;
using LiteNetLib;
using LiteNetLib.Utils;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Server.Services.Shutdown;

/// <summary>Drives <see cref="CoopServer.OnConnectionRequest"/>, <see cref="CoopServer.OnPeerConnected"/> and the client reject popup over loopback.</summary>
public class ServerAdmissionGateTests
{
    private const string Password = "join-gate-test";
    private const string RestartingPopup = "The server is restarting. Try again in a few minutes.";
    private const string PasswordPopup = "The server password is incorrect.";
    private const string SaveName = "MP";

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

    [Fact]
    public void TryAdmit_RegistersOnlyWhileTheGateIsOpen()
    {
        var gate = new ServerAdmissionGate();
        int registered = 0;

        Assert.True(gate.TryAdmit(() => registered++));
        gate.Close();
        Assert.False(gate.TryAdmit(() => registered++));
        gate.Open();
        Assert.True(gate.TryAdmit(() => registered++));

        Assert.Equal(2, registered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PeerAcceptedWhileOpen_RegisteringAfterTheShutdownEnded_IsDisconnectedBeforeItJoins(bool saved)
    {
        using var race = new RegistrationRace();
        race.ConnectAndHoldRegistration();

        race.ScheduleShutdownNow();
        Assert.Equal(ServerShutdownPhase.Saving, race.Coordinator.Phase);
        if (saved) race.CompleteSave();
        else race.ServerBroker.Publish(new object(), new GameSaveCompleted(SaveName, false));
        var ended = saved ? ServerShutdownPhase.Completed : ServerShutdownPhase.Failed;
        Assert.Equal(ended, race.Coordinator.Phase);

        race.Register();
        race.PumpUntil(() => race.ClientDisconnectReason != null);

        Assert.Equal(ServerShutdownCoordinator.DisconnectReason, race.ClientDisconnectReason);
        Assert.Equal(new[] { "save queued", "disconnected" }, race.ServerEvents);
        Assert.Empty(race.Connections);
        Assert.Equal(ended, race.Coordinator.Phase);
    }

    [Fact]
    public async Task RegistrationOverlappingTheGateClosing_IsDrainedBeforeTheSave()
    {
        using var race = new RegistrationRace();
        race.ConnectAndHoldRegistration();

        using var registering = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Action<MessagePayload<ConnectionStateChanged>> pause = _ =>
        {
            registering.Set();
            release.Wait(TimeSpan.FromSeconds(5));
        };
        race.ServerBroker.Subscribe(pause);

        // The connection logic enters its first state before it is added to the collection.
        var registration = Task.Run(race.Register);
        Assert.True(registering.Wait(TimeSpan.FromSeconds(5)));
        var shutdown = Task.Run(race.ScheduleShutdownNow);

        await Task.WhenAny(shutdown, Task.Delay(300));
        Assert.False(shutdown.IsCompleted, "The shutdown got past the gate while a registration was still running.");
        release.Set();
        await Task.WhenAll(registration, shutdown).WaitAsync(TimeSpan.FromSeconds(5));

        race.PumpUntil(() => race.ClientDisconnectReason != null && !race.Connections.Any());
        // Starts the save if the server only saw the disconnect during the pump.
        race.Coordinator.Tick();
        race.CompleteSave();

        Assert.Equal(ServerShutdownPhase.Completed, race.Coordinator.Phase);
        Assert.Equal(ServerShutdownCoordinator.DisconnectReason, race.ClientDisconnectReason);
        Assert.Equal(new[] { "registered", "disconnected", "save queued" }, race.ServerEvents);
        GC.KeepAlive(pause);
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
        using var server = CreateServer(new TestMessageBroker(), gate, cancellation);

        RunLoopback(broker, server.OnConnectionRequest, suppliedPassword, complete, session);
    }

    private static CoopServer CreateServer(TestMessageBroker broker, IServerAdmissionGate gate, CancellationTokenSource session) => new CoopServer(
        CreateConfig(Password).Object,
        broker,
        Mock.Of<IPacketManager>(),
        Mock.Of<IMessagePacketHandler>(),
        Mock.Of<IConnectionMessageQueue>(),
        gate,
        new JoinPeerTerminator(),
        Mock.Of<IControllerIdProvider>(),
        Mock.Of<IMissionManager>(),
        new Lazy<IOverloadedPeerManager>(() => Mock.Of<IOverloadedPeerManager>()),
        Mock.Of<ISendCoalescer>(),
        Mock.Of<ICommonSerializer>(),
        Mock.Of<IReliableMessageBatcher<NetPeer>>(),
        session);

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

    /// <summary>
    /// A real server and client over loopback where the test decides when the accepted peer registers,
    /// with a real <see cref="ConnectionCollection"/> and shutdown coordinator behind the server.
    /// </summary>
    private sealed class RegistrationRace : IDisposable
    {
        public readonly TestMessageBroker ServerBroker = new TestMessageBroker();
        public readonly ConnectionCollection Connections;
        public readonly ServerShutdownCoordinator Coordinator;
        public readonly ConcurrentQueue<string> ServerEvents = new ConcurrentQueue<string>();

        private readonly TestMessageBroker clientBroker = new TestMessageBroker();
        private readonly CancellationTokenSource serverSession = new CancellationTokenSource();
        private readonly CancellationTokenSource clientSession = new CancellationTokenSource();
        private readonly CoopServer server;
        private readonly CoopClient client;
        private readonly NetManager serverTransport;
        private readonly NetManager clientTransport;
        private readonly Action<MessagePayload<PlayerConnected>> recordRegistration;
        private NetPeer? acceptedPeer;

        public RegistrationRace()
        {
            var gate = new ServerAdmissionGate();
            server = CreateServer(ServerBroker, gate, serverSession);
            Connections = new ConnectionCollection(ServerBroker, CreateConnectionContext(ServerBroker));
            // Subscribed after the collection, so it runs once the peer is in it.
            recordRegistration = _ => ServerEvents.Enqueue("registered");
            ServerBroker.Subscribe(recordRegistration);

            var saves = new Mock<ISaveInterface>();
            saves.SetupGet(value => value.CanQueueSave).Returns(true);
            saves.Setup(value => value.TryQueueSave(It.IsAny<string>()))
                .Callback(() => ServerEvents.Enqueue("save queued"))
                .Returns(true);
            Coordinator = new ServerShutdownCoordinator(
                ServerBroker,
                Mock.Of<INetwork>(),
                Connections,
                new JoinPeerTerminator(),
                gate,
                saves.Object,
                Mock.Of<IMissionManager>(),
                () => DateTime.UtcNow,
                _ => Mock.Of<IDisposable>(),
                action => action());

            // LiteNetLib reports the accepted peer here, and the test decides when the server registers it.
            var serverListener = new EventBasedNetListener();
            serverListener.ConnectionRequestEvent += server.OnConnectionRequest;
            serverListener.PeerConnectedEvent += peer => acceptedPeer = peer;
            serverListener.PeerDisconnectedEvent += (peer, info) =>
            {
                ServerEvents.Enqueue("disconnected");
                server.OnPeerDisconnected(peer, info);
            };
            serverTransport = new NetManager(serverListener);

            client = new CoopClient(CreateConfig(null).Object, clientBroker, Mock.Of<IPacketManager>(),
                Mock.Of<IMessagePacketHandler>(), Mock.Of<ICommonSerializer>(),
                Mock.Of<IReliableMessageBatcher<NetPeer>>(), clientSession);
            clientTransport = new NetManager(client);
        }

        public string? ClientDisconnectReason =>
            clientBroker.GetMessagesFromType<NetworkDisconnected>().SingleOrDefault()?.ServerReason;

        /// <summary>Connects until the server has accepted the request, without registering the peer.</summary>
        public void ConnectAndHoldRegistration()
        {
            Assert.True(serverTransport.StartInManualMode(0));
            Assert.True(clientTransport.StartInManualMode(0));
            clientTransport.Connect(IPAddress.Loopback.ToString(), serverTransport.LocalPort, Password);

            PumpUntil(() => acceptedPeer != null && clientBroker.GetMessagesFromType<NetworkConnected>().Any());
            Assert.Empty(ServerBroker.GetMessagesFromType<PlayerConnected>());
        }

        public void Register() => server.OnPeerConnected(acceptedPeer!);

        public void ScheduleShutdownNow()
        {
            Assert.True(Coordinator.TrySchedule(0, SaveName, out string result), result);
        }

        public void CompleteSave()
        {
            ServerBroker.Publish(new object(), new CoopSessionWritten(SaveName, true));
            ServerBroker.Publish(new object(), new GameSaveCompleted(SaveName, true));
        }

        public void PumpUntil(Func<bool> complete) =>
            ServerAdmissionGateTests.PumpUntil(serverTransport, clientTransport, clientSession.Token, complete);

        public void Dispose()
        {
            Coordinator.Dispose();
            Connections.Dispose();
            clientTransport.Stop();
            serverTransport.Stop();
            client.Dispose();
            server.Dispose();
            clientSession.Dispose();
            serverSession.Dispose();
        }

        private static ConnectionContext CreateConnectionContext(TestMessageBroker broker) => new ConnectionContext(
            broker,
            Mock.Of<INetwork>(),
            Mock.Of<IModuleValidator>(),
            Mock.Of<IModuleInfoProvider>(),
            Mock.Of<IPlayerManager>(),
            Mock.Of<IPlayerPartyRestorer>(),
            Mock.Of<IPlayerCreationRollback>(),
            Mock.Of<IObjectManager>(),
            Mock.Of<IHeroInterface>(),
            Mock.Of<ICoopSessionProvider>(),
            Mock.Of<ISaveInterface>(),
            Mock.Of<IConnectionMessageQueue>(),
            Mock.Of<ISendCoalescer>(),
            Mock.Of<IAttachmentIdMapper>(),
            Mock.Of<IExistingPlayerSender>(),
            Mock.Of<ISteamBanList>(),
            Mock.Of<IServerOptionsProvider>(),
            Mock.Of<IJoinCampaignBaselineSender>(),
            Mock.Of<IJoinCampaignKingdomBaseLineSender>());
    }
}
