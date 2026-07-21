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

/// <summary>Regression coverage for bounded movement traffic and delivery selection.</summary>
public class MovementTrafficTests : MissionTestEnvironment
{
    public MovementTrafficTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PollMovement_UsesTwentyHertzCadenceAndThreeAgentBatches()
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
                Assert.True(registry.TryRegisterAgent("peer", Guid.NewGuid(), SpawnRider(mock)));

            component.AgentMovementHandler.PollMovement(0f);
            Assert.Equal(new[] { 3, 1 }, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Select(packet => packet.AgentIds.Length));

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(0.049f);
            Assert.Empty(network.NetworkSentPackets.GetPackets<MovementPacket>());

            component.AgentMovementHandler.PollMovement(0.002f);
            Assert.Equal(new[] { 3, 1 }, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Select(packet => packet.AgentIds.Length));
        });
    }

    [Fact]
    public void PollMovement_BoundsAndRotatesLargeArmiesWhileAlwaysSendingMainAgent()
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
            var agentIds = new List<Guid>();

            for (int i = 0; i < 125; i++)
            {
                Agent agent = SpawnRider(mock);
                Guid agentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent("peer", agentId, agent));
                agentIds.Add(agentId);
                if (i == 124) mock.MainAgent = agent;
            }

            component.AgentMovementHandler.PollMovement(0f);
            Guid[] firstPoll = network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .SelectMany(packet => packet.AgentIds)
                .ToArray();
            Assert.Equal(120, firstPoll.Length);
            Assert.Equal(firstPoll.Length, firstPoll.Distinct().Count());
            Assert.Contains(agentIds[124], firstPoll);
            Assert.All(agentIds.Skip(119).Take(5), id => Assert.DoesNotContain(id, firstPoll));

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(0.05f);
            Guid[] secondPoll = network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .SelectMany(packet => packet.AgentIds)
                .ToArray();
            Assert.Equal(120, secondPoll.Length);
            Assert.Equal(secondPoll.Length, secondPoll.Distinct().Count());
            Assert.Contains(agentIds[124], secondPoll);
            Assert.All(agentIds.Skip(119).Take(5), id => Assert.Contains(id, secondPoll));
            Assert.Equal(agentIds.Count, firstPoll.Concat(secondPoll).Distinct().Count());
        });
    }

    [Fact]
    public void ThreeMountedSnapshots_FitDirectAndRelayDatagrams()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var ids = new Guid[3];
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
                ids[i] = Guid.NewGuid();
                var mountId = Guid.NewGuid();
                riders[i] = new AgentData(rider, mountId);
                mounts[i] = new AgentMountData(mount, mountId);
            }

            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            AssertFitsRelay(serializer, new MovementPacket(ids, riders));
            AssertFitsRelay(serializer, new MountMovementPacket(ids, mounts));
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

    private static void AssertFitsRelay(ProtoBufSerializer serializer, IPacket packet)
    {
        byte[] payload = serializer.Serialize(packet);
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
