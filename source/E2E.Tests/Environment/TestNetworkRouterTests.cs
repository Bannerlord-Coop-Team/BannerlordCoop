using Common;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network.Packets;
using E2E.Tests.Environment.Instance;
using GameInterface;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using ProtoBuf;
using Xunit.Abstractions;

namespace E2E.Tests.Environment;

public class TestNetworkRouterTests : E2ETestEnvironment
{
    private readonly List<Delegate> liveSubscriptions = new();

    public TestNetworkRouterTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void MessageBroadcast_UsesDistinctWireCopiesAndDropsUnserializedState()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        var original = new WireProbeMessage
        {
            Included = "wire-value",
            LocalOnly = "must-not-cross",
        };

        Subscribe<WireProbeMessage>(clients[0], payload => payload.What.Included = "receiver-a-mutated");

        Server.Call(() => Server.Resolve<INetwork>().SendAll(original));

        WireProbeMessage receivedA = Assert.Single(
            clients[0].InternalMessages.GetMessages<WireProbeMessage>());
        WireProbeMessage receivedB = Assert.Single(
            clients[1].InternalMessages.GetMessages<WireProbeMessage>());
        Assert.NotSame(original, receivedA);
        Assert.NotSame(original, receivedB);
        Assert.NotSame(receivedA, receivedB);
        Assert.Equal("receiver-a-mutated", receivedA.Included);
        Assert.Equal("wire-value", receivedB.Included);
        Assert.Equal("wire-value", original.Included);
        Assert.Null(receivedA.LocalOnly);
        Assert.Null(receivedB.LocalOnly);
    }

    [Fact]
    public void BufferedMessages_UseProductionAggregateAndReceiveHandlers()
    {
        EnvironmentInstance client = Clients.First();
        Mock.MockServer network = Server.Resolve<Mock.MockServer>();

        Server.Call(() =>
        {
            INetwork sender = Server.Resolve<INetwork>();
            sender.Send(client.NetPeer, new WireProbeMessage
            {
                Included = "first",
                LocalOnly = "must-not-cross"
            });
            sender.Send(client.NetPeer, new WireProbeMessage
            {
                Included = "second",
                LocalOnly = "must-not-cross"
            });

            Assert.Empty(client.InternalMessages.GetMessages<WireProbeMessage>());
            sender.FlushPendingMessages();
        });

        AggregateMessagePacket aggregate = Assert.Single(
            network.NetworkSentPackets.GetPackets<AggregateMessagePacket>());
        Assert.Equal(2, aggregate.Messages.Length);
        WireProbeMessage[] received = client.InternalMessages
            .GetMessages<WireProbeMessage>()
            .ToArray();
        Assert.Equal(new[] { "first", "second" }, received.Select(message => message.Included));
        Assert.All(received, message => Assert.Null(message.LocalOnly));
    }

    [Fact]
    public void SendImmediateMessagePacket_RetainsProductionAggregationBehavior()
    {
        EnvironmentInstance client = Clients.First();
        INetwork sender = Server.Resolve<INetwork>();
        MessagePacket packet = MessagePacket.Create(
            new WireProbeMessage { Included = "framed" },
            Server.Resolve<ICommonSerializer>());

        Server.Call(() =>
        {
            sender.SendImmediate(client.NetPeer, packet);
            Assert.Empty(client.InternalMessages.GetMessages<WireProbeMessage>());
        });

        Assert.Equal(
            "framed",
            Assert.Single(client.InternalMessages.GetMessages<WireProbeMessage>()).Included);
    }

    [Fact]
    public void PacketBroadcast_UsesDistinctWireCopiesAndDropsUnserializedState()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        var receiverA = new RecordingPacketHandler();
        var receiverB = new RecordingPacketHandler();
        IPacketManager managerA = clients[0].Resolve<IPacketManager>();
        IPacketManager managerB = clients[1].Resolve<IPacketManager>();
        managerA.RegisterPacketHandler(receiverA);
        managerB.RegisterPacketHandler(receiverB);

        try
        {
            var original = new WireProbePacket
            {
                Included = 42,
                LocalOnly = "must-not-cross",
            };

            Server.Call(() => Server.Resolve<INetwork>().SendAll(original));

            WireProbePacket receivedA = Assert.IsType<WireProbePacket>(Assert.Single(receiverA.Packets));
            WireProbePacket receivedB = Assert.IsType<WireProbePacket>(Assert.Single(receiverB.Packets));
            Assert.NotSame(original, receivedA);
            Assert.NotSame(original, receivedB);
            Assert.NotSame(receivedA, receivedB);
            Assert.Equal(42, receivedA.Included);
            Assert.Equal(42, receivedB.Included);
            Assert.Null(receivedA.LocalOnly);
            Assert.Null(receivedB.LocalOnly);
        }
        finally
        {
            managerA.RemovePacketHandler(receiverA);
            managerB.RemovePacketHandler(receiverB);
        }
    }

    [Fact]
    public void DefaultBatching_AllowsSequencedPacketToOvertakeMessageBeforeNetworkTick()
    {
        EnvironmentInstance client = Clients.First();
        var packetHandler = new RecordingPacketHandler();
        IPacketManager packetManager = client.Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(packetHandler);

        try
        {
            Server.Call(() =>
            {
                INetwork sender = Server.Resolve<INetwork>();
                sender.Send(client.NetPeer, new WireProbeMessage { Included = "buffered" });
                sender.Send(client.NetPeer, new WireProbePacket
                {
                    Included = 7,
                    DeliveryMethod = DeliveryMethod.Sequenced,
                });

                Assert.Empty(client.InternalMessages.GetMessages<WireProbeMessage>());
                Assert.Equal(7, Assert.IsType<WireProbePacket>(Assert.Single(packetHandler.Packets)).Included);
            });

            Assert.Equal(
                "buffered",
                Assert.Single(client.InternalMessages.GetMessages<WireProbeMessage>()).Included);
        }
        finally
        {
            packetManager.RemovePacketHandler(packetHandler);
        }
    }

    [Fact]
    public void BlockingGameThreadWait_PumpsSimulatedNetworkTick()
    {
        EnvironmentInstance client = Clients.First();
        bool serverReceived = false;
        Subscribe<WireProbeMessage>(Server, _ => serverReceived = true);

        client.Call(() =>
        {
            client.Resolve<INetwork>().Send(
                Server.NetPeer,
                new WireProbeMessage { Included = "blocking-request" });

            Assert.True(GameThread.WaitWhilePumping(
                () => serverReceived,
                DateTime.UtcNow + TimeSpan.FromSeconds(1)));
        });

        Assert.True(serverReceived);
    }

    [Fact]
    public void DefaultReceiveContext_AppliesGameThreadWorkInline()
    {
        EnvironmentInstance client = Clients.First();
        int applied = 0;
        Subscribe<QueuedApplyMessage>(client, _ => GameThread.RunSafe(() => applied++));

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 1 }));

        Assert.Equal(TestNetworkReceiveContext.GameThread, Router.ReceiveContext);
        Assert.Equal(1, applied);
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    [Fact]
    public void PollerReceive_QueuesWorkUntilRecipientPumpAndRestoresOuterGameThread()
    {
        EnvironmentInstance client = Clients.First();
        int previousGameThreadId = GameThread.Instance.GameThreadId;
        bool receivedOffGameThread = false;
        int applied = 0;
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Subscribe<QueuedApplyMessage>(client, _ =>
        {
            receivedOffGameThread = !GameThread.Instance.IsGameThread;
            GameThread.RunSafe(() => applied++);
        });

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 1 }));

        Assert.True(receivedOffGameThread);
        Assert.Equal(previousGameThreadId, GameThread.Instance.GameThreadId);
        Assert.Equal(0, applied);
        Assert.Equal(1, client.PendingGameThreadActionCount);

        Assert.Equal(1, client.PumpGameThread(maximumPasses: 1));
        Assert.Equal(1, applied);
        Assert.Equal(0, client.PendingGameThreadActionCount);
        Assert.Equal(previousGameThreadId, GameThread.Instance.GameThreadId);
    }

    [Fact]
    public void PumpGameThread_DrainsPollerFollowUpCreatedByNetworkFlush()
    {
        EnvironmentInstance client = Clients.First();
        int appliedReplies = 0;
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Subscribe<QueuedApplyMessage>(client, payload => GameThread.RunSafe(() =>
            client.Resolve<INetwork>().Send(
                Server.NetPeer,
                new PumpFollowUpRequest { Value = payload.What.Value })));
        Subscribe<PumpFollowUpRequest>(Server, payload =>
            Server.Resolve<INetwork>().Send(
                client.NetPeer,
                new PumpFollowUpReply { Value = payload.What.Value }));
        Subscribe<PumpFollowUpReply>(client, payload => GameThread.RunSafe(() =>
            appliedReplies += payload.What.Value));

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 5 }));

        Assert.Equal(1, client.PendingGameThreadActionCount);
        Assert.Equal(2, client.PumpGameThread(maximumPasses: 2));
        Assert.Equal(5, appliedReplies);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public void PollerReceive_CreateThenUpdate_ResolvesInsideFifoGameThreadActions()
    {
        const string objectId = "wire-replica";
        EnvironmentInstance client = Clients.First();
        bool pollerLookupFound = true;
        bool createApplied = false;
        bool updateApplied = false;
        bool usedRecipientContainer = false;
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;

        Subscribe<CreateReplicaMessage>(client, payload => GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;

            usedRecipientContainer = ReferenceEquals(objectManager, client.ObjectManager);
            createApplied = objectManager.AddExisting(payload.What.ObjectId!, new WireReplica());
        }));
        Subscribe<UpdateReplicaMessage>(client, payload =>
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var pollerObjectManager)) return;

            pollerLookupFound = pollerObjectManager.TryGetObject<WireReplica>(
                payload.What.ObjectId!,
                out _);
            GameThread.RunSafe(() =>
            {
                if (!ContainerProvider.TryResolve<IObjectManager>(out var gameThreadObjectManager)) return;
                if (!gameThreadObjectManager.TryGetObject<WireReplica>(payload.What.ObjectId!, out var replica))
                    return;

                replica.Value = payload.What.Value;
                updateApplied = true;
            });
        });

        Server.Call(() =>
        {
            INetwork network = Server.Resolve<INetwork>();
            network.Send(client.NetPeer, new CreateReplicaMessage { ObjectId = objectId });
            network.Send(client.NetPeer, new UpdateReplicaMessage { ObjectId = objectId, Value = 73 });
        });

        Assert.False(pollerLookupFound);
        Assert.False(client.ObjectManager.Contains(objectId));
        Assert.False(createApplied);
        Assert.False(updateApplied);
        Assert.Equal(2, client.PendingGameThreadActionCount);

        Assert.Equal(1, client.PumpGameThread(maximumPasses: 1));

        WireReplica replica = client.GetRegisteredObject<WireReplica>(objectId);
        Assert.True(usedRecipientContainer);
        Assert.True(createApplied);
        Assert.True(updateApplied);
        Assert.Equal(73, replica.Value);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public void PollerReceive_TwoRecipientsKeepSeparateGameThreadQueuesAndStatics()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        EnvironmentInstance clientA = clients[0];
        EnvironmentInstance clientB = clients[1];
        IObjectManager? appliedWithA = null;
        IObjectManager? appliedWithB = null;
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;

        Subscribe<QueuedApplyMessage>(clientA, _ => GameThread.RunSafe(() =>
        {
            ContainerProvider.TryResolve<IObjectManager>(out appliedWithA);
        }));
        Subscribe<QueuedApplyMessage>(clientB, _ => GameThread.RunSafe(() =>
        {
            ContainerProvider.TryResolve<IObjectManager>(out appliedWithB);
        }));

        Server.Call(() => Server.Resolve<INetwork>().SendAll(
            new QueuedApplyMessage { Value = 1 }));

        Assert.Equal(1, clientA.PendingGameThreadActionCount);
        Assert.Equal(1, clientB.PendingGameThreadActionCount);
        Assert.Equal(0, GameThread.Instance.QueueLength);

        Assert.Equal(1, clientA.PumpGameThread(maximumPasses: 1));
        Assert.Same(clientA.ObjectManager, appliedWithA);
        Assert.Null(appliedWithB);
        Assert.Equal(0, clientA.PendingGameThreadActionCount);
        Assert.Equal(1, clientB.PendingGameThreadActionCount);

        Assert.Equal(1, clientB.PumpGameThread(maximumPasses: 1));
        Assert.Same(clientB.ObjectManager, appliedWithB);
        Assert.Equal(0, clientB.PendingGameThreadActionCount);
    }

    [Fact]
    public void UnpumpedRecipientWork_IsReleasedAndReportedBeforeTeardown()
    {
        EnvironmentInstance client = Clients.First();
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Subscribe<QueuedApplyMessage>(client, _ => GameThread.RunSafe(() => { }));

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 1 }));

        Assert.Equal(1, client.PendingGameThreadActionCount);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            client.ReleasePendingGameThreadActions);
        Assert.Contains("1 unpumped game-thread action", exception.Message);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public async Task ClosedRecipientQueue_RejectsLateAsyncContinuations()
    {
        EnvironmentInstance client = Clients.First();
        var resumeContinuation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Exception?>? blockingContinuation = null;
        Task? nonblockingContinuation = null;
        int applied = 0;

        client.Call(() =>
        {
            blockingContinuation = Task.Run(async () =>
            {
                await resumeContinuation.Task;
                return Record.Exception(() => GameThread.Run(() => applied++, blocking: true));
            });
            nonblockingContinuation = Task.Run(async () =>
            {
                await resumeContinuation.Task;
                GameThread.RunSafe(() => applied++);
            });
        });

        client.ReleasePendingGameThreadActions();
        resumeContinuation.SetResult(true);

        Exception? blockingFailure = await blockingContinuation!;
        await nonblockingContinuation!;

        Assert.IsType<OperationCanceledException>(blockingFailure);
        Assert.Equal(0, applied);
        Assert.Equal(2, client.RejectedGameThreadActionCount);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public async Task ClosingRecipientQueue_CancelsAlreadyQueuedBlockingAction()
    {
        EnvironmentInstance client = Clients.First();
        Task<Exception?>? blockingAction = null;
        int applied = 0;

        client.Call(() =>
        {
            blockingAction = Task.Run(() =>
                Record.Exception(() => GameThread.Run(() => applied++, blocking: true)));
        });

        Assert.True(SpinWait.SpinUntil(
            () => client.PendingGameThreadActionCount == 1,
            TimeSpan.FromSeconds(5)));
        InvalidOperationException teardownFailure = Assert.Throws<InvalidOperationException>(
            client.ReleasePendingGameThreadActions);

        Exception? blockingFailure = await blockingAction!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains("1 unpumped game-thread action", teardownFailure.Message);
        Assert.IsType<OperationCanceledException>(blockingFailure);
        Assert.Equal(0, applied);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public async Task ThrowingDequeuedAction_CancelsTrailingBlockingActionWithoutTimeout()
    {
        EnvironmentInstance client = Clients.First();
        Task<Exception?>? trailingBlockingAction = null;
        int trailingApplied = 0;

        client.Call(() =>
        {
            trailingBlockingAction = Task.Run(() =>
            {
                GameThread.Run(() => throw new InvalidOperationException("expected first failure"));
                return Record.Exception(() =>
                    GameThread.Run(() => trailingApplied++, blocking: true));
            });
        });

        Assert.True(SpinWait.SpinUntil(
            () => client.PendingGameThreadActionCount == 2,
            TimeSpan.FromSeconds(5)));

        InvalidOperationException pumpFailure = Assert.Throws<InvalidOperationException>(
            () => client.PumpGameThread(maximumPasses: 1));
        Exception? blockingFailure = await trailingBlockingAction!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("expected first failure", pumpFailure.Message);
        Assert.IsType<OperationCanceledException>(blockingFailure);
        Assert.Equal(0, trailingApplied);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public async Task DisposalBoundary_WaitsForDequeuedGameThreadActionToFinish()
    {
        EnvironmentInstance client = Clients.First();
        using var actionStarted = new ManualResetEventSlim();
        using var allowActionToFinish = new ManualResetEventSlim();
        using var disposalAttempted = new ManualResetEventSlim();
        using var resourcesDisposed = new ManualResetEventSlim();
        int actionTimedOut = 0;
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Subscribe<QueuedApplyMessage>(client, _ => GameThread.RunSafe(() =>
        {
            actionStarted.Set();
            if (allowActionToFinish.Wait(TimeSpan.FromSeconds(10)) == false)
                Interlocked.Exchange(ref actionTimedOut, 1);
        }));

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 1 }));

        Assert.Equal(1, client.PendingGameThreadActionCount);
        Task<int> pumpTask = Task.Run(() => client.PumpGameThread(maximumPasses: 1));
        Assert.True(actionStarted.Wait(TimeSpan.FromSeconds(5)));

        Task disposalTask = Task.Factory.StartNew(
            () =>
            {
                disposalAttempted.Set();
                client.CloseGameThreadQueueAndDispose(resourcesDisposed.Set);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Assert.True(disposalAttempted.Wait(TimeSpan.FromSeconds(5)));

        try
        {
            Assert.False(resourcesDisposed.Wait(TimeSpan.FromMilliseconds(250)));
            Assert.False(disposalTask.IsCompleted);
        }
        finally
        {
            allowActionToFinish.Set();
        }

        Assert.Equal(1, await pumpTask.WaitAsync(TimeSpan.FromSeconds(5)));
        await disposalTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(resourcesDisposed.IsSet);
        Assert.Equal(0, actionTimedOut);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    [Fact]
    public void ManualDelivery_AllowsLegalCrossChannelReordering()
    {
        EnvironmentInstance client = Clients.First();
        var packetHandler = new RecordingPacketHandler();
        var packetTypeOnlyHandler = new RecordingPacketHandler(PacketType.SaveData);
        var saveChunkHandler = new RecordingPacketHandler(PacketType.SaveDataChunk);
        IPacketManager packetManager = client.Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(packetHandler);
        packetManager.RegisterPacketHandler(packetTypeOnlyHandler);
        packetManager.RegisterPacketHandler(saveChunkHandler);
        Router.AutoDrainReady = false;
        Router.SetLatency(Server.NetPeer, client.NetPeer, TimeSpan.FromMilliseconds(100));

        try
        {
            Server.Call(() => Server.Resolve<INetwork>().Send(
                client.NetPeer,
                new QueuedApplyMessage { Value = 1 }));

            Router.SetLatency(Server.NetPeer, client.NetPeer, TimeSpan.Zero);
            Server.Call(() => Server.Resolve<INetwork>().Send(
                client.NetPeer,
                new WireProbePacket
                {
                    Included = 2,
                    DeliveryMethod = DeliveryMethod.Unreliable,
                }));
            Server.Call(() => Server.Resolve<INetwork>().Send(
                client.NetPeer,
                new WireProbePacket
                {
                    Included = 3,
                    PacketType = PacketType.SaveData,
                    DeliveryMethod = DeliveryMethod.ReliableOrdered,
                }));
            Server.Call(() => Server.Resolve<INetwork>().Send(
                client.NetPeer,
                CreateSaveChunkPacket(4)));

            Assert.Empty(client.InternalMessages.GetMessages<QueuedApplyMessage>());
            Assert.Empty(packetHandler.Packets);
            Assert.Empty(packetTypeOnlyHandler.Packets);
            Assert.Empty(saveChunkHandler.Packets);
            Assert.Equal(4, Router.PendingDeliveryCount);

            Assert.Equal(2, Router.DrainReady());
            WireProbePacket packet = Assert.IsType<WireProbePacket>(Assert.Single(packetHandler.Packets));
            Assert.Equal(2, packet.Included);
            GameSaveDataChunkPacket saveChunk = Assert.IsType<GameSaveDataChunkPacket>(
                Assert.Single(saveChunkHandler.Packets));
            Assert.Equal(4, saveChunk.TransferId);
            Assert.Empty(packetTypeOnlyHandler.Packets);
            Assert.Empty(client.InternalMessages.GetMessages<QueuedApplyMessage>());

            Assert.Equal(2, Router.AdvanceBy(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(1, Assert.Single(client.InternalMessages.GetMessages<QueuedApplyMessage>()).Value);
            WireProbePacket packetTypeOnly = Assert.IsType<WireProbePacket>(
                Assert.Single(packetTypeOnlyHandler.Packets));
            Assert.Equal(3, packetTypeOnly.Included);
            Assert.Contains(Router.Trace, entry => entry.Channel == "ReliableOrdered:channel-0");
            Assert.Contains(Router.Trace, entry => entry.Channel == "Unreliable:channel-0");
            Assert.Contains(Router.Trace, entry => entry.Channel == "ReliableOrdered:channel-1");
        }
        finally
        {
            packetManager.RemovePacketHandler(packetHandler);
            packetManager.RemovePacketHandler(packetTypeOnlyHandler);
            packetManager.RemovePacketHandler(saveChunkHandler);
        }
    }

    private static GameSaveDataChunkPacket CreateSaveChunkPacket(int transferId) =>
        new GameSaveDataChunkPacket(
            transferId,
            0,
            1,
            1,
            1,
            new byte[] { 1 },
            "router-test",
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

    [Fact]
    public void ReliableOrderedWorldTraffic_RemainsFifoAcrossMessagesAndPackets()
    {
        EnvironmentInstance client = Clients.First();
        var deliveryOrder = new List<string>();
        var packetHandler = new RecordingPacketHandler(_ => deliveryOrder.Add("packet"));
        IPacketManager packetManager = client.Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(packetHandler);
        Subscribe<QueuedApplyMessage>(client, _ => deliveryOrder.Add("message"));
        Router.AutoDrainReady = false;
        Router.SetLatency(Server.NetPeer, client.NetPeer, TimeSpan.FromMilliseconds(100));

        try
        {
            Server.Call(() => Server.Resolve<INetwork>().Send(
                client.NetPeer,
                new QueuedApplyMessage { Value = 1 }));

            Router.SetLatency(Server.NetPeer, client.NetPeer, TimeSpan.Zero);
            Server.Call(() => Server.Resolve<INetwork>().Send(
                client.NetPeer,
                new WireProbePacket
                {
                    Included = 2,
                    DeliveryMethod = DeliveryMethod.ReliableOrdered,
                }));

            Assert.Equal(0, Router.AdvanceBy(TimeSpan.FromMilliseconds(99)));
            Assert.Empty(deliveryOrder);

            Assert.Equal(2, Router.AdvanceBy(TimeSpan.FromMilliseconds(1)));
            Assert.Equal(new[] { "message", "packet" }, deliveryOrder);
        }
        finally
        {
            packetManager.RemovePacketHandler(packetHandler);
        }
    }

    [Fact]
    public void Disconnect_CancelsStaleTrafficBeforeReconnect()
    {
        EnvironmentInstance client = Clients.First();
        Router.AutoDrainReady = false;
        Router.SetLatency(Server.NetPeer, client.NetPeer, TimeSpan.FromMilliseconds(50));
        long initialGeneration = Router.GetConnectionGeneration(client.NetPeer);

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 1 }));

        Assert.Equal(1, Router.Disconnect(client.NetPeer));
        Assert.False(Router.IsConnected(client.NetPeer));
        long disconnectedGeneration = Router.GetConnectionGeneration(client.NetPeer);
        Assert.True(disconnectedGeneration > initialGeneration);
        Assert.Equal(0, Router.PendingDeliveryCount);

        Router.Reconnect(client.NetPeer);
        Assert.True(Router.IsConnected(client.NetPeer));
        Assert.True(Router.GetConnectionGeneration(client.NetPeer) > disconnectedGeneration);
        Router.SetLatency(Server.NetPeer, client.NetPeer, TimeSpan.Zero);

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 2 }));

        Assert.Equal(1, Router.DrainReady());
        QueuedApplyMessage delivered = Assert.Single(
            client.InternalMessages.GetMessages<QueuedApplyMessage>());
        Assert.Equal(2, delivered.Value);
        Assert.Contains(Router.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Canceled);
        Assert.Contains(Router.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Disconnected);
        Assert.Contains(Router.Trace, entry => entry.Kind == VirtualNetworkTraceKind.Reconnected);
    }

    [Fact]
    public void Disconnect_DiscardsBufferedMessagesFromBothSidesBeforeReconnect()
    {
        EnvironmentInstance client = Clients.First();
        Mock.MockServer serverNetwork = Server.Resolve<Mock.MockServer>();
        Mock.MockClient clientNetwork = client.Resolve<Mock.MockClient>();

        serverNetwork.Send(
            client.NetPeer,
            new WireProbeMessage { Included = "stale-server" });
        clientNetwork.Send(
            Server.NetPeer,
            new WireProbeMessage { Included = "stale-client" });

        Assert.Equal(0, Router.Disconnect(client.NetPeer));

        serverNetwork.Send(
            client.NetPeer,
            new WireProbeMessage { Included = "stale-server-disconnected" });
        clientNetwork.Send(
            Server.NetPeer,
            new WireProbeMessage { Included = "stale-client-disconnected" });

        Router.Reconnect(client.NetPeer);

        Server.Call(() =>
        {
            serverNetwork.Send(client.NetPeer, new WireProbeMessage { Included = "fresh-server" });
            serverNetwork.FlushPendingMessages();
        });
        client.Call(() =>
        {
            clientNetwork.Send(Server.NetPeer, new WireProbeMessage { Included = "fresh-client" });
            clientNetwork.FlushPendingMessages();
        });

        Assert.Equal(
            "fresh-server",
            Assert.Single(client.InternalMessages.GetMessages<WireProbeMessage>()).Included);
        Assert.Equal(
            "fresh-client",
            Assert.Single(Server.InternalMessages.GetMessages<WireProbeMessage>()).Included);
    }

    [Fact]
    public void Broadcast_SkipsDisconnectedRecipientAndReachesConnectedRecipient()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        EnvironmentInstance disconnected = clients[0];
        EnvironmentInstance connected = clients[1];
        Router.Disconnect(disconnected.NetPeer);

        Server.Call(() => Server.Resolve<INetwork>().SendAll(
            new WireProbeMessage { Included = "connected-only" }));

        Assert.Empty(disconnected.InternalMessages.GetMessages<WireProbeMessage>());
        Assert.Equal(
            "connected-only",
            Assert.Single(connected.InternalMessages.GetMessages<WireProbeMessage>()).Included);

        Router.Reconnect(disconnected.NetPeer);
    }

    [Fact]
    public void ManualPollerDelivery_PublishesBeforeExplicitRecipientPump()
    {
        EnvironmentInstance client = Clients.First();
        bool published = false;
        int applied = 0;
        Router.AutoDrainReady = false;
        Router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Subscribe<QueuedApplyMessage>(client, _ =>
        {
            published = true;
            GameThread.RunSafe(() => applied++);
        });

        Server.Call(() => Server.Resolve<INetwork>().Send(
            client.NetPeer,
            new QueuedApplyMessage { Value = 1 }));

        Assert.False(published);
        Assert.Equal(0, GameThread.Instance.QueueLength);
        Assert.Equal(1, Router.PendingDeliveryCount);

        Assert.Equal(1, Router.DrainReady());
        Assert.True(published);
        Assert.Equal(0, applied);
        Assert.Equal(1, client.PendingGameThreadActionCount);

        Assert.Equal(1, client.PumpGameThread(maximumPasses: 1));
        Assert.Equal(1, applied);
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }

    private TestNetworkRouter Router => Server.Resolve<TestNetworkRouter>();

    private void Subscribe<T>(EnvironmentInstance instance, Action<MessagePayload<T>> subscription)
        where T : IMessage
    {
        liveSubscriptions.Add(subscription);
        instance.Resolve<IMessageBroker>().Subscribe(subscription);
    }

    [ProtoContract]
    private sealed class WireProbeMessage : IMessage
    {
        [ProtoMember(1)]
        public string? Included { get; set; }

        public string? LocalOnly { get; set; }
    }

    [ProtoContract]
    private sealed class WireProbePacket : IPacket
    {
        [ProtoMember(3)]
        public PacketType PacketType { get; set; } = PacketType.Test;

        [ProtoMember(2)]
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.ReliableOrdered;

        [ProtoMember(1)]
        public int Included { get; set; }

        public string? LocalOnly { get; set; }
    }

    [ProtoContract]
    private sealed class QueuedApplyMessage : IMessage
    {
        [ProtoMember(1)]
        public int Value { get; set; }
    }

    [ProtoContract]
    private sealed class PumpFollowUpRequest : IMessage
    {
        [ProtoMember(1)]
        public int Value { get; set; }
    }

    [ProtoContract]
    private sealed class PumpFollowUpReply : IMessage
    {
        [ProtoMember(1)]
        public int Value { get; set; }
    }

    [ProtoContract]
    private sealed class CreateReplicaMessage : IMessage
    {
        [ProtoMember(1)]
        public string? ObjectId { get; set; }
    }

    [ProtoContract]
    private sealed class UpdateReplicaMessage : IMessage
    {
        [ProtoMember(1)]
        public string? ObjectId { get; set; }

        [ProtoMember(2)]
        public int Value { get; set; }
    }

    private sealed class WireReplica
    {
        public int Value { get; set; }
    }

    private sealed class RecordingPacketHandler : IPacketHandler
    {
        private readonly Action<IPacket>? onPacket;

        public PacketType PacketType { get; }
        public List<IPacket> Packets { get; } = new();

        public RecordingPacketHandler(Action<IPacket>? onPacket = null)
            : this(PacketType.Test, onPacket)
        {
        }

        public RecordingPacketHandler(PacketType packetType, Action<IPacket>? onPacket = null)
        {
            PacketType = packetType;
            this.onPacket = onPacket;
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            Packets.Add(packet);
            onPacket?.Invoke(packet);
        }

        public void Dispose()
        {
        }
    }
}
