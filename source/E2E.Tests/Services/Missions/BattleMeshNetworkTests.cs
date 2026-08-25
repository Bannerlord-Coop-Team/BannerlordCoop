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
        public PacketType PacketType => PacketType.Movement;
        public int HandleCount => Packets.Count;
        public List<IPacket> Packets { get; } = new();

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            Packets.Add(packet);
        }

        public void Dispose()
        {
        }
    }
}
