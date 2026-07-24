using System;
using System.Linq;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using LiteNetLib;
using Missions;
using Missions.Agents;
using Missions.Agents.Packets;
using Missions.Services.Network;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for movement traffic and delivery selection.</summary>
public class MovementTrafficTests : MissionTestEnvironment
{
    private readonly ITestOutputHelper output;

    public MovementTrafficTests(ITestOutputHelper output) : base(output)
    {
        this.output = output;
    }

    [Fact]
    public void PollMovement_UsesFortyHertzCadenceAndThreeAgentBatches()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());

            for (int i = 0; i < 4; i++)
                Assert.True(registry.TryRegisterAgent(
                    "peer", Guid.NewGuid(), (ushort)(i + 1), SpawnRider(mock)));

            component.AgentMovementHandler.PollMovement(0f);
            Assert.Equal(new[] { 3, 1 }, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Select(packet => packet.AgentIds.Length));

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(0.024f);
            Assert.Empty(network.NetworkSentPackets.GetPackets<MovementPacket>());

            component.AgentMovementHandler.PollMovement(0.002f);
            Assert.Equal(new[] { 3, 1 }, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Select(packet => packet.AgentIds.Length));

            network.NetworkSentPackets.Packets.Clear();
            for (int i = 0; i < 6; i++)
                component.AgentMovementHandler.PollMovement(1f / 60f);
            Assert.Equal(16, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Sum(packet => packet.AgentIds.Length));
        });
    }

    [Fact]
    public void PollMovement_SendsEveryEligibleAgent()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            var movementIds = new List<ushort>();

            for (int i = 0; i < 125; i++)
            {
                Agent agent = SpawnRider(mock);
                Guid agentId = Guid.NewGuid();
                ushort movementId = (ushort)(i + 1);
                Assert.True(registry.TryRegisterAgent(
                    "peer", agentId, movementId, agent));
                movementIds.Add(movementId);
            }

            component.AgentMovementHandler.PollMovement(0f);
            MovementPacket[] packets = network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .ToArray();
            Assert.Equal((movementIds.Count + 2) / 3, packets.Length);
            Assert.All(packets, packet => Assert.InRange(packet.AgentIds.Length, 1, 3));

            ushort[] sentAgents = packets
                .SelectMany(packet => packet.AgentIds)
                .ToArray();
            Assert.Equal(movementIds.Count, sentAgents.Length);
            Assert.Equal(sentAgents.Length, sentAgents.Distinct().Count());
            Assert.All(movementIds, id => Assert.Contains(id, sentAgents));
        });
    }

    [Fact]
    public void PollMovement_SeedsSpawnEquipmentAndOnlySendsChanges()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(registry.TryRegisterAgent(
                "peer", Guid.NewGuid(), 1, agent));

            component.AgentMovementHandler.PollMovement(0f);
            Assert.Empty(
                network.NetworkSentPackets.GetPackets<AgentEquipmentPacket>());

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Empty(
                network.NetworkSentPackets.GetPackets<AgentEquipmentPacket>());

            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon2;
            component.AgentMovementHandler.PollMovement(0.025f);
            var changed = Assert.Single(
                network.NetworkSentPackets.GetPackets<AgentEquipmentPacket>());
            Assert.Equal("peer", changed.IdentityScopeId);
            Assert.Equal(new ushort[] { 1 }, changed.AgentIds);
        });
    }

    [Fact]
    public void PollMovement_SendsInitialEquipmentForLegacyGuidAgents()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            Guid agentId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("peer", agentId, SpawnRider(mock)));

            component.AgentMovementHandler.PollMovement(0f);

            var initial = Assert.Single(
                network.NetworkSentPackets.GetPackets<AgentEquipmentPacket>());
            Assert.Equal(new[] { agentId }, initial.AgentGuids);
        });
    }

    [Fact]
    public void Lz4MovementCompression_RoundTripsRepresentativeThreeAgentPacket()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var agents = new AgentData[3];
            for (int i = 0; i < agents.Length; i++)
            {
                Agent rider = SpawnRider(mock);
                Assert.True(AgentMirror.TryGet(rider, out var mirror));
                PopulateMovementState(mirror, i + 1);
                agents[i] = new AgentData(rider);
            }

            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var compressor = new MovementPacketCompressor(serializer);
            var movement = new MovementPacket(
                "76561198000000042",
                new ushort[] { 1, 2, 3 },
                agents);

            byte[] original = serializer.Serialize(movement);
            byte[] wire = compressor.Serialize(movement);
            output.WriteLine(
                $"Three-agent movement packet: {original.Length} bytes compact, {wire.Length} bytes LZ4");

            Assert.Equal(wire.Length, compressor.GetSerializedLength(movement));
            Assert.True(wire.Length < original.Length,
                $"LZ4 envelope was {wire.Length} bytes for {original.Length} input bytes");
            IPacket envelope = serializer.Deserialize<IPacket>(wire);
            Assert.IsType<CompressedMovementPacket>(envelope);
            Assert.True(compressor.TryRestore(envelope, out var restored));

            var roundTripped = Assert.IsType<MovementPacket>(restored);
            Assert.Equal(movement.IdentityScopeId, roundTripped.IdentityScopeId);
            Assert.Equal(movement.AgentIds, roundTripped.AgentIds);
            Assert.Equal(movement.Agents.Length, roundTripped.Agents.Length);
            Assert.True(wire.Length <= LiteNetP2PClient.SafeSinglePacketBytes);
        });
    }

    [Fact]
    public void Lz4MovementCompression_RejectsCorruptEnvelope()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var compressor = new MovementPacketCompressor(serializer);
        var corrupt = new CompressedMovementPacket(512, new byte[] { 1, 2, 3 });

        Assert.False(compressor.TryRestore(corrupt, out _));
    }

    [Fact]
    public void ThreeMountedSnapshots_FitDirectAndRelayDatagrams()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var ids = new ushort[3];
            var riders = new AgentData[3];
            var mounts = new AgentMountData[3];

            for (int i = 0; i < ids.Length; i++)
            {
                var rider = SpawnRider(mock);
                var mount = mock.SpawnMount(rider);
                Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
                Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
                PopulateMovementState(riderMirror, i + 1);
                PopulateMovementState(mountMirror, i + 1);
                ids[i] = (ushort)(i + 1);
                ushort mountId = (ushort)(i + 101);
                riders[i] = new AgentData(rider, mountId);
                mounts[i] = new AgentMountData(mount, mountId);
            }

            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var compressor = new MovementPacketCompressor(serializer);
            AssertFitsRelay(serializer, compressor,
                new MovementPacket("76561198000000042", ids, riders));
            AssertFitsRelay(serializer, compressor,
                new MountMovementPacket("76561198000000042", ids, mounts));
        });
    }

    [Fact]
    public void OversizedMovement_IsDroppedInsteadOfPromotedToReliable()
    {
        int datagramCeiling = LiteNetP2PClient.SafeSinglePacketBytes;
        var movement = new MovementPacket(Array.Empty<Guid>(), Array.Empty<AgentData>());
        var mountMovement = new MountMovementPacket(Array.Empty<Guid>(), Array.Empty<AgentMountData>());
        var ordinaryUnreliable = new TestPacket(PacketType.Test, DeliveryMethod.Unreliable);
        var reliable = new TestPacket(PacketType.Test, DeliveryMethod.ReliableOrdered);

        Assert.Equal(DeliveryMethod.Unreliable,
            LiteNetP2PClient.SelectDeliveryMethod(movement, datagramCeiling, datagramCeiling));
        Assert.Null(LiteNetP2PClient.SelectDeliveryMethod(movement, datagramCeiling + 1, datagramCeiling));
        Assert.Null(LiteNetP2PClient.SelectDeliveryMethod(mountMovement, datagramCeiling + 1, datagramCeiling));
        Assert.Equal(DeliveryMethod.ReliableUnordered,
            LiteNetP2PClient.SelectDeliveryMethod(ordinaryUnreliable, datagramCeiling + 1, datagramCeiling));
        Assert.Equal(DeliveryMethod.ReliableOrdered,
            LiteNetP2PClient.SelectDeliveryMethod(reliable, datagramCeiling + 1, datagramCeiling));

        Assert.Null(LiteNetP2PClient.SelectDeliveryMethod(movement, datagramCeiling - 1, 0));
        Assert.Equal(DeliveryMethod.ReliableUnordered,
            LiteNetP2PClient.SelectDeliveryMethod(ordinaryUnreliable, datagramCeiling - 1, 0));
    }

    private static void AssertFitsRelay(
        ProtoBufSerializer serializer,
        MovementPacketCompressor compressor,
        IPacket packet)
    {
        byte[] payload = compressor.Serialize(packet);
        Assert.True(payload.Length <= LiteNetP2PClient.SafeSinglePacketBytes,
            $"Direct payload was {payload.Length} bytes");

        byte[] relay = serializer.Serialize(new RelayPacket(
            packet.DeliveryMethod,
            "MapEvent_Created_0000",
            "76561198000000042",
            payload));
        Assert.True(relay.Length <= LiteNetP2PClient.SafeSinglePacketBytes,
            $"Relay payload was {relay.Length} bytes");
    }

    private static Agent SpawnRider(MockMission mock)
    {
        return mock.SpawnAgent(
            new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
    }

    private static void PopulateMovementState(MirrorAgent mirror, int multiplier)
    {
        mirror.Position = new Vec3(1024.25f * multiplier, -2048.5f * multiplier, 512.75f * multiplier);
        mirror.InputVector = new Vec2(0.75f, -0.5f);
        mirror.LookDirection = new Vec3(-0.25f, 0.5f, 0.75f);
        mirror.MovementDirection = new Vec2(-0.75f, 0.5f);
        mirror.RealGlobalVelocity = new Vec3(123.25f, -456.5f, 789.75f);
        mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon3;
        mirror.OffhandWieldedItemIndex = EquipmentIndex.Weapon2;
        mirror.Action0Flags = (AnimFlags)ulong.MaxValue;
        mirror.Action1Flags = (AnimFlags)ulong.MaxValue;
        mirror.Action0Progress = 0.75f;
        mirror.Action1Progress = 0.5f;
        mirror.Action0Index = -1;
        mirror.Action1Index = -1;
    }

    private readonly struct TestPacket : IPacket
    {
        public PacketType PacketType { get; }
        public DeliveryMethod DeliveryMethod { get; }

        public TestPacket(PacketType packetType, DeliveryMethod deliveryMethod)
        {
            PacketType = packetType;
            DeliveryMethod = deliveryMethod;
        }
    }
}
