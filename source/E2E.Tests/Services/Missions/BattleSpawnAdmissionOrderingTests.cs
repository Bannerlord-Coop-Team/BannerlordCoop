using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.Extensions;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System.Reflection;

namespace E2E.Tests.Services.Missions;

public class BattleSpawnAdmissionOrderingTests
{
    [Fact]
    public void AdmittedSpawnReservesItsQueuePositionBeforeDepartureAndMultipleMigrations()
    {
        using var fixture = new Fixture();
        using var deserialized = new ManualResetEventSlim();
        using var departureStarted = new ManualResetEventSlim();
        fixture.AfterDeserialize = deserialized.Set;
        Task receive = Task.CompletedTask;
        Task departure = Task.CompletedTask;
        try
        {
            lock (fixture.Queue.gate)
            {
                receive = Task.Run(fixture.ReceiveSpawn);
                Assert.True(deserialized.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(() =>
                {
                    if (!Monitor.TryEnter(fixture.PeerGate)) return true;
                    Monitor.Exit(fixture.PeerGate);
                    return false;
                }, TimeSpan.FromSeconds(5)), "Spawn admission did not hold the peer lock while reserving its queue position.");
                departure = Task.Run(() =>
                {
                    departureStarted.Set();
                    fixture.Broker.Publish(this, new MissionPeerDisconnected(Fixture.RemoteController, Fixture.InstanceId));
                    fixture.Broker.Publish(this, new BattleHostMigrated(Fixture.InstanceId, Fixture.RemoteController, "next-host"));
                    fixture.Broker.Publish(this, new BattleHostMigrated(Fixture.InstanceId, "next-host", "last-host"));
                });
                Assert.True(departureStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(departure.IsCompleted);
                Assert.Empty(fixture.Applied);
            }
        }
        finally
        {
            Assert.True(Task.WaitAll(new[] { receive, departure }, TimeSpan.FromSeconds(5)));
        }
        Assert.Empty(fixture.Applied);
        Assert.Equal(0, fixture.PublishedSpawns);
        Assert.False(fixture.Context.TryGetPeer(Fixture.RemoteController, out _));
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { "spawn", "departure", "next-host", "last-host" }, fixture.Applied);
        Assert.Equal(1, fixture.PublishedSpawns);
        Assert.True(fixture.SpawnPublishedOnGameThread);
        Assert.False(fixture.SpawnPublishedUnderPeerLock);
        Assert.Equal("last-host", fixture.CurrentAuthority);
        Assert.Equal(2, fixture.AuthorityRevision);
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    [Theory]
    [InlineData("departure")]
    [InlineData("same-instance-reentry")]
    [InlineData("different-instance")]
    [InlineData("credential-rotation")]
    public void InvalidationDuringDecodeRejectsTheSpawnBeforeQueueAdmission(string invalidation)
    {
        using var fixture = new Fixture();
        using var deserialized = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        fixture.AfterDeserialize = () =>
        {
            deserialized.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Spawn decode was not released.");
        };
        Task receive = Task.Run(fixture.ReceiveSpawn);
        try
        {
            Assert.True(deserialized.Wait(TimeSpan.FromSeconds(5)));
            fixture.Invalidate(invalidation);
        }
        finally
        {
            resume.Set();
            Assert.True(receive.Wait(TimeSpan.FromSeconds(5)));
        }
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(0, fixture.PublishedSpawns);
        Assert.DoesNotContain("spawn", fixture.Applied);
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueuedSpawnFromAnEarlierLocalMissionDoesNotPublishIntoItsReplacement(bool sameInstance)
    {
        using var fixture = new Fixture();
        fixture.ReceiveSpawn();
        Assert.Equal(0, fixture.PublishedSpawns);
        Assert.Equal(1, GameThread.Instance.QueueLength);

        fixture.Invalidate(sameInstance ? "same-instance-reentry" : "different-instance");
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(0, fixture.PublishedSpawns);
        Assert.Empty(fixture.Applied);
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    private sealed class Fixture : IDisposable
    {
        public const string InstanceId = "battle-spawn-admission";
        public const string RemoteController = "departing-host";
        public GameThread.QueueContext Queue { get; } = new();
        public MessageBroker Broker { get; } = new();
        public MissionContext Context { get; }
        public LiteNetP2PClient Client { get; }
        public NetPeer Peer { get; } = NetPeerExtensions.CreatePeer(109);
        public object PeerGate { get; }
        public List<string> Applied { get; } = new();
        public Action? AfterDeserialize { get; set; }
        public int PublishedSpawns { get; private set; }
        public bool SpawnPublishedOnGameThread { get; private set; }
        public bool SpawnPublishedUnderPeerLock { get; private set; }
        public string? CurrentAuthority { get; private set; }
        public int AuthorityRevision { get; private set; }
        private readonly int previousGameThreadId;
        private readonly IDisposable queueScope;
        private readonly byte[] spawnPayload;
        private readonly MessagePacketHandler messageHandler;

        public Fixture()
        {
            previousGameThreadId = GameThread.Instance.GameThreadId;
            queueScope = GameThread.ActivateQueue(Queue);
            GameThread.Instance.MarkGameThread();
            var wireSerializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var serializer = new Mock<ICommonSerializer>();
            serializer.Setup(value => value.Serialize(It.IsAny<object>()))
                .Returns<object>(wireSerializer.Serialize);
            serializer.Setup(value => value.Deserialize(It.IsAny<byte[]>()))
                .Returns<byte[]>(data =>
                {
                    object decoded = wireSerializer.Deserialize(data);
                    AfterDeserialize?.Invoke();
                    return decoded;
                });
            var controllerIdProvider = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "local-observer");
            Context = new MissionContext(Broker, controllerIdProvider);
            var packetManager = new PacketManager();
            messageHandler = new MessagePacketHandler(Broker, packetManager, serializer.Object);
            Client = new LiteNetP2PClient(
                Mock.Of<INetworkConfig>(value => value.IsTunneled),
                Mock.Of<IRelayNetwork>(), Context, serializer.Object, Broker,
                packetManager, messageHandler, controllerIdProvider,
                Mock.Of<ISteamMissionBridge>(), new MovementPacketCompressor(serializer.Object),
                new ReliableMessageBatcher<string>(serializer.Object), () => new ReceivePathDiagnostics());
            Client.ConnectToInstance(InstanceId);
            PeerGate = GetField<object>("peerGate");
            Guid credential = Guid.NewGuid();
            GetField<Dictionary<NetPeer, string>>("pendingPeerControllers")[Peer] = RemoteController;
            GetField<Dictionary<NetPeer, Guid>>("peerCredentials")[Peer] = credential;
            Broker.Publish(this, new NetworkMissionPeerEntered(RemoteController, InstanceId, 0, credential));
            Client.OnPeerConnected(Peer);
            Assert.True(Context.TryGetPeer(RemoteController, out _));
            Broker.Subscribe<NetworkSpawnBattleAgents>(HandleSpawn);
            Broker.Subscribe<MissionPeerDisconnected>(HandleDeparture);
            Broker.Subscribe<BattleHostMigrated>(HandleMigration);
            spawnPayload = wireSerializer.Serialize(new NetworkSpawnBattleAgents(Array.Empty<BattleAgentSpawnData>()));
        }

        public void ReceiveSpawn()
        {
            var reader = ObjectHelper.SkipConstructor<NetPacketReader>();
            reader.SetSource(spawnPayload);
            Client.OnNetworkReceive(Peer, reader, 0, DeliveryMethod.ReliableOrdered);
        }

        public void Invalidate(string reason)
        {
            switch (reason)
            {
                case "departure":
                    Broker.Publish(this, new MissionPeerDisconnected(RemoteController, InstanceId));
                    break;
                case "same-instance-reentry":
                    Client.DisconnectPeers();
                    Client.ConnectToInstance(InstanceId);
                    break;
                case "different-instance":
                    Client.ConnectToInstance("replacement-battle");
                    break;
                case "credential-rotation":
                    Broker.Publish(this, new NetworkMissionPeerEntered(RemoteController, InstanceId, 0, Guid.NewGuid()));
                    break;
                default:
                    throw new ArgumentException("Unknown invalidation", nameof(reason));
            }
        }

        private void HandleSpawn(MessagePayload<NetworkSpawnBattleAgents> payload)
        {
            PublishedSpawns++;
            SpawnPublishedOnGameThread = GameThread.Instance.IsGameThread;
            SpawnPublishedUnderPeerLock = Monitor.IsEntered(PeerGate);
            GameThread.RunSafe(() =>
            {
                Applied.Add("spawn");
                CurrentAuthority = RemoteController;
            });
        }

        private void HandleDeparture(MessagePayload<MissionPeerDisconnected> payload) =>
            GameThread.RunSafe(() => Applied.Add("departure"));

        private void HandleMigration(MessagePayload<BattleHostMigrated> payload) =>
            GameThread.RunSafe(() =>
            {
                Applied.Add(payload.What.NewHostControllerId);
                if (CurrentAuthority != payload.What.PreviousHostControllerId) return;
                CurrentAuthority = payload.What.NewHostControllerId;
                AuthorityRevision++;
            });

        private T GetField<T>(string name) => (T)typeof(LiteNetP2PClient)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Client)!;

        public void Dispose()
        {
            try
            {
                Client.Dispose();
                Context.Dispose();
                messageHandler.Dispose();
                Broker.Dispose();
                GameThread.Instance.DiscardQueuedActions();
            }
            finally
            {
                queueScope.Dispose();
                GameThread.Instance.RestoreGameThread(previousGameThreadId);
            }
        }
    }
}
