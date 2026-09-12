using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using LiteNetLib;
using Missions;
using Missions.Agents.Packets;
using Missions.Messages;
using System;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Validates lifecycle, instance membership, virtual delivery, and packet/message routing in the in-process mesh.
/// </summary>
public class BattleMeshNetworkTests : MissionTestEnvironment
{
    public BattleMeshNetworkTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    private static NetworkSpawnBattleAgents EmptySpawn() =>
        new NetworkSpawnBattleAgents(Array.Empty<BattleAgentSpawnData>());

    [Fact]
    public void Mesh_SendAll_DeliversToOtherMembers_ButNotSender()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        Connect(clients[2], "ctrl-C", "instance-2");

        NetworkSpawnBattleAgents message = EmptySpawn();
        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().SendAll(message));

        Assert.Equal(0, clients[0].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
        NetworkSpawnBattleAgents received = Assert.Single(
            clients[1].InternalMessages.GetMessages<NetworkSpawnBattleAgents>());
        Assert.NotSame(message, received);
        Assert.Equal(0, clients[2].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
    }

    [Fact]
    public void Mesh_Send_DeliversOnlyToTargetControllerInSameInstance()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-2");
        Connect(clients[2], "ctrl-C", "instance-1");

        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().Send("ctrl-B", EmptySpawn()));
        Assert.Equal(0, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());

        Mesh(clients[1]).ConnectToInstance("instance-1");
        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().Send("ctrl-B", EmptySpawn()));

        Assert.Equal(1, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
        Assert.Equal(0, clients[2].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
    }

    [Fact]
    public void Mesh_LatencyDefersDelivery_AndStopCancelsPendingTraffic()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(25);

        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().SendAll(EmptySpawn()));
        Assert.Equal(0, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
        Assert.Equal(0, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(24)));

        Mesh(clients[0]).Stop();
        Assert.Equal(0, scheduler.PendingDeliveryCount);
        Assert.Equal(0, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1)));
        Assert.Equal(0, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());

        Mesh(clients[0]).Start();
        Mesh(clients[0]).ConnectToInstance("instance-1");
        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().SendAll(EmptySpawn()));

        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(25)));
        Assert.Equal(1, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
    }

    [Fact]
    public void Mesh_ReceiverInstanceSwitch_CancelsTrafficScheduledUnderOldMembership()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(25);

        clients[0].Call(() => Mesh(clients[0]).SendAll(new NetworkBattleActivated("old-membership")));
        Assert.Equal(1, scheduler.PendingDeliveryCount);

        Mesh(clients[1]).ConnectToInstance("instance-2");
        Mesh(clients[1]).ConnectToInstance("instance-1");

        Assert.Equal(0, scheduler.PendingDeliveryCount);
        clients[0].Call(() => Mesh(clients[0]).SendAll(new NetworkBattleActivated("new-membership")));
        Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(25)));

        NetworkBattleActivated received = Assert.Single(
            clients[1].InternalMessages.GetMessages<NetworkBattleActivated>());
        Assert.Equal("new-membership", received.MapEventId);
    }

    [Fact]
    public void Mesh_MessageChannel_RemainsFifoWhenLatencyDrops()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(100);

        clients[0].Call(() => Mesh(clients[0]).SendAll(new NetworkBattleActivated("first")));
        scheduler.DefaultLatency = TimeSpan.Zero;
        clients[0].Call(() => Mesh(clients[0]).SendAll(new NetworkBattleActivated("second")));

        Assert.Empty(clients[1].InternalMessages.GetMessages<NetworkBattleActivated>());
        Assert.Equal(2, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(
            new[] { "first", "second" },
            clients[1].InternalMessages
                .GetMessages<NetworkBattleActivated>()
                .Select(message => message.MapEventId));
    }

    [Fact]
    public void Mesh_UnreliablePacketChannel_AllowsLaterLowLatencyPacketToOvertake()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(100);
        var receiver = new RecordingPacketHandler(PacketType.CompressedMovement);
        IPacketManager packetManager = clients[1].Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(receiver);

        try
        {
            clients[0].Call(() => Mesh(clients[0]).SendAll(
                new CompressedMovementPacket(1, new byte[] { 1 })));
            scheduler.DefaultLatency = TimeSpan.Zero;
            clients[0].Call(() => Mesh(clients[0]).SendAll(
                new CompressedMovementPacket(1, new byte[] { 2 })));

            Assert.Equal(1, receiver.HandleCount);
            Assert.Equal(2, Assert.IsType<CompressedMovementPacket>(receiver.Packets.Single()).Payload[0]);
            Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(
                new byte[] { 2, 1 },
                receiver.Packets
                    .Select(packet => Assert.IsType<CompressedMovementPacket>(packet).Payload[0]));
        }
        finally
        {
            packetManager.RemovePacketHandler(receiver);
        }
    }

    [Fact]
    public void Mesh_PacketDeliveryMethods_AdvanceIndependently()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(100);
        MockBattleNetwork sender = Mesh(clients[0]);
        MockBattleNetwork receiverMesh = Mesh(clients[1]);
        var unreliableReceiver = new RecordingPacketHandler(PacketType.CompressedMovement);
        var orderedReceiver = new RecordingPacketHandler(PacketType.AgentAction);
        IPacketManager packetManager = clients[1].Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(unreliableReceiver);
        packetManager.RegisterPacketHandler(orderedReceiver);

        try
        {
            clients[0].Call(() => sender.SendAll(
                new CompressedMovementPacket(1, new byte[] { 1 })));
            scheduler.SetLatency(sender, receiverMesh, TimeSpan.FromMilliseconds(5));
            clients[0].Call(() => sender.SendAll(new AgentActionPacket(
                "ctrl-A",
                Array.Empty<Guid>(),
                Array.Empty<AgentActionData>(),
                Array.Empty<long>())));

            Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5)));
            Assert.Equal(0, unreliableReceiver.HandleCount);
            Assert.Equal(1, orderedReceiver.HandleCount);

            Assert.Equal(1, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(95)));
            Assert.Equal(1, unreliableReceiver.HandleCount);
        }
        finally
        {
            packetManager.RemovePacketHandler(unreliableReceiver);
            packetManager.RemovePacketHandler(orderedReceiver);
        }
    }

    [Fact]
    public void Mesh_UnknownDirectRecipient_DoesNotScheduleMessageOrPacket()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        MockBattleNetwork sender = Mesh(clients[0]);
        var packet = new MovementPacket(Array.Empty<Guid>(), Array.Empty<AgentData>());

        clients[0].Call(() =>
        {
            sender.Send("missing-controller", new NetworkBattleActivated("not-delivered"));
            sender.Send("missing-controller", packet);
        });

        Assert.Equal(0, scheduler.PendingDeliveryCount);
        Assert.Empty(clients[1].InternalMessages.GetMessages<NetworkBattleActivated>());
        Assert.Equal(1, sender.NetworkSentMessages.GetMessageCount<NetworkBattleActivated>());
        Assert.Equal(1, sender.NetworkSentPackets.GetPacketCount<MovementPacket>());
        DirectPacketSend directSend = Assert.Single(sender.DirectPacketSends);
        Assert.Equal("missing-controller", directSend.ControllerId);
    }

    [Fact]
    public void Mesh_NonEmptySerializedPayload_RoutesDeserializedWireValues()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        var receiver = new RecordingPacketHandler(PacketType.CompressedMovement);
        IPacketManager packetManager = clients[1].Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(receiver);

        try
        {
            var packet = new CompressedMovementPacket(12, new byte[] { 2, 4, 6, 8 });
            var wirePacket = new CompressedMovementPacket(34, new byte[] { 9, 7, 5, 3 });
            byte[] serializedPayload = clients[0].Resolve<ICommonSerializer>().Serialize(wirePacket);
            MockBattleNetwork sender = Mesh(clients[0]);

            clients[0].Call(() => sender.SendAll(packet, serializedPayload));

            CompressedMovementPacket received = Assert.IsType<CompressedMovementPacket>(
                Assert.Single(receiver.Packets));
            Assert.Equal(wirePacket.UncompressedLength, received.UncompressedLength);
            Assert.Equal(wirePacket.Payload, received.Payload);
            Assert.NotEqual(packet.Payload, received.Payload);
            SerializedPacketSend captured = Assert.Single(sender.SerializedPacketSends);
            Assert.Equal(serializedPayload, captured.Payload);
        }
        finally
        {
            packetManager.RemovePacketHandler(receiver);
        }
    }

    [Fact]
    public void Mesh_ReliableMessageAndPacket_ShareOrderingWhenLatencyDrops()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        IVirtualNetworkScheduler scheduler = clients[0].Resolve<IVirtualNetworkScheduler>();
        scheduler.DefaultLatency = TimeSpan.FromMilliseconds(100);

        var deliveries = new List<string>();
        clients[1].Resolve<IMessageBroker>()
            .Subscribe<NetworkSpawnBattleAgents>(_ => deliveries.Add("message"));
        var packetHandler = new RecordingPacketHandler(
            PacketType.AgentAction,
            () => deliveries.Add("packet"));
        IPacketManager packetManager = clients[1].Resolve<IPacketManager>();
        packetManager.RegisterPacketHandler(packetHandler);

        try
        {
            clients[0].Call(() =>
                clients[0].Resolve<IBattleNetwork>().Send("ctrl-B", EmptySpawn()));
            scheduler.DefaultLatency = TimeSpan.Zero;
            clients[0].Call(() =>
                clients[0].Resolve<IBattleNetwork>().Send(
                    "ctrl-B",
                    new AgentActionPacket(
                        "ctrl-A",
                        Array.Empty<Guid>(),
                        Array.Empty<AgentActionData>(),
                        Array.Empty<long>())));

            Assert.Empty(deliveries);
            Assert.Equal(0, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(99)));
            Assert.Equal(2, scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1)));
            Assert.Equal(new[] { "message", "packet" }, deliveries);
        }
        finally
        {
            packetManager.RemovePacketHandler(packetHandler);
        }
    }

    [Fact]
    public void Mesh_AllPacketOverloads_RouteAndPreserveSendCapture()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        Connect(clients[2], "ctrl-C", "instance-1");

        var receiverB = new RecordingPacketHandler();
        var receiverC = new RecordingPacketHandler();
        IPacketManager packetManagerB = clients[1].Resolve<IPacketManager>();
        IPacketManager packetManagerC = clients[2].Resolve<IPacketManager>();
        packetManagerB.RegisterPacketHandler(receiverB);
        packetManagerC.RegisterPacketHandler(receiverC);

        try
        {
            var packet = new MovementPacket(Array.Empty<Guid>(), Array.Empty<AgentData>());
            byte[] serializedPacket = clients[0].Resolve<ICommonSerializer>().Serialize(packet);
            MockBattleNetwork sender = Mesh(clients[0]);

            clients[0].Call(() =>
            {
                sender.SendAll(packet);
                sender.SendAll(packet, serializedPacket);
                sender.Send("ctrl-B", packet);
                sender.Send("ctrl-B", packet, serializedPacket);
                sender.SendAllBut("ctrl-B", packet);
            });

            Assert.Equal(4, receiverB.HandleCount);
            Assert.Equal(3, receiverC.HandleCount);
            Assert.All(receiverB.Packets, received =>
                Assert.NotSame(packet.AgentGuids, Assert.IsType<MovementPacket>(received).AgentGuids));
            Assert.All(receiverC.Packets, received =>
                Assert.NotSame(packet.AgentGuids, Assert.IsType<MovementPacket>(received).AgentGuids));
            Assert.Equal(5, sender.NetworkSentPackets.Count);
            Assert.Equal(2, sender.DirectPacketSends.Count);
            Assert.Equal(2, sender.SerializedPacketSends.Count);
        }
        finally
        {
            packetManagerB.RemovePacketHandler(receiverB);
            packetManagerC.RemovePacketHandler(receiverC);
        }
    }

    [Fact]
    public void Mesh_SerializedPacketOverloads_RouteProvidedPayloadThroughRestore()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        Connect(clients[0], "ctrl-A", "instance-1");
        Connect(clients[1], "ctrl-B", "instance-1");
        Connect(clients[2], "ctrl-C", "instance-1");

        var receiverB = new RecordingPacketHandler();
        var receiverC = new RecordingPacketHandler();
        IPacketManager packetManagerB = clients[1].Resolve<IPacketManager>();
        IPacketManager packetManagerC = clients[2].Resolve<IPacketManager>();
        packetManagerB.RegisterPacketHandler(receiverB);
        packetManagerC.RegisterPacketHandler(receiverC);

        try
        {
            var logicalPacket = new MovementPacket(Array.Empty<Guid>(), Array.Empty<AgentData>());
            var corruptEnvelope = new CompressedMovementPacket(512, new byte[] { 1, 2, 3 });
            byte[] serializedPacket = clients[0].Resolve<ICommonSerializer>().Serialize(corruptEnvelope);
            MockBattleNetwork sender = Mesh(clients[0]);

            clients[0].Call(() =>
            {
                sender.SendAll(logicalPacket, serializedPacket);
                sender.Send("ctrl-B", logicalPacket, serializedPacket);
            });

            Assert.Empty(receiverB.Packets);
            Assert.Empty(receiverC.Packets);
            Assert.Equal(2, sender.SerializedPacketSends.Count);
            Assert.All(sender.SerializedPacketSends, send => Assert.Same(serializedPacket, send.Payload));
        }
        finally
        {
            packetManagerB.RemovePacketHandler(receiverB);
            packetManagerC.RemovePacketHandler(receiverC);
        }
    }

    private void Connect(EnvironmentInstance client, string controllerId, string instanceId)
    {
        SetControllerId(client, controllerId);
        MockBattleNetwork mesh = Mesh(client);
        mesh.Start();
        mesh.ConnectToInstance(instanceId);
    }

    private static MockBattleNetwork Mesh(EnvironmentInstance client) =>
        Assert.IsType<MockBattleNetwork>(client.Resolve<IBattleNetwork>());

    private sealed class RecordingPacketHandler : IPacketHandler
    {
        private readonly Action? onHandle;

        public PacketType PacketType { get; }
        public int HandleCount => Packets.Count;
        public List<IPacket> Packets { get; } = new();

        public RecordingPacketHandler(
            PacketType packetType = PacketType.Movement,
            Action? onHandle = null)
        {
            PacketType = packetType;
            this.onHandle = onHandle;
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            Packets.Add(packet);
            onHandle?.Invoke();
        }

        public void Dispose()
        {
        }
    }
}
