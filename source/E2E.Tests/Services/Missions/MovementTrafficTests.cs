using System;
using System.Linq;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Services.Network;
using Moq;
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
    public void PollMovement_UsesFortyHertzCadenceAndSkipsUnchangedAgents()
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
            var mirrors = new List<MirrorAgent>();

            for (int i = 0; i < 4; i++)
            {
                Agent agent = SpawnRider(mock);
                Assert.True(AgentMirror.TryGet(agent, out var mirror));
                mirrors.Add(mirror);
                Assert.True(registry.TryRegisterAgent(
                    "peer", Guid.NewGuid(), (ushort)(i + 1), agent));
            }

            component.AgentMovementHandler.PollMovement(0f);
            Assert.Equal(new[] { 4 }, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Select(packet => packet.AgentIds.Length));

            network.NetworkSentPackets.Packets.Clear();
            foreach (MirrorAgent mirror in mirrors)
                mirror.Position = new Vec3(1f, 0f, 0f);

            component.AgentMovementHandler.PollMovement(0.024f);
            Assert.Empty(network.NetworkSentPackets.GetPackets<MovementPacket>());

            component.AgentMovementHandler.PollMovement(0.002f);
            Assert.Equal(new[] { 4 }, network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .Select(packet => packet.AgentIds.Length));

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Empty(network.NetworkSentPackets.GetPackets<MovementPacket>());
        });
    }

    [Fact]
    public void PollMovement_SendsNonPositionalChangesAndHeartbeat()
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
            Assert.True(registry.TryRegisterAgent("peer", Guid.NewGuid(), 1, agent));

            component.AgentMovementHandler.PollMovement(0f);
            Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Empty(network.NetworkSentPackets.GetPackets<MovementPacket>());

            mirror.LookDirection = new Vec3(0f, 1f, 0f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());

            network.NetworkSentPackets.Packets.Clear();
            mirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());

            network.NetworkSentPackets.Packets.Clear();
            mirror.RealGlobalVelocity = Vec3.Zero;
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());

            network.NetworkSentPackets.Packets.Clear();
            component.AgentMovementHandler.PollMovement(1f);
            Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());
        });
    }

    [Fact]
    public void PollMovement_BatchesIncompressibleSnapshotsAndSendsEveryAgent()
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
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            network.MaxUnreliablePayloadBytes = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
                serializer,
                "MapEvent_Created_0000",
                "76561198000000042",
                LiteNetP2PClient.SafeSinglePacketBytes);

            for (int i = 0; i < 125; i++)
            {
                Agent agent = SpawnRider(mock);
                Assert.True(AgentMirror.TryGet(agent, out var mirror));
                PopulateIncompressibleMovementState(mirror, i + 1);
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
            Assert.True(packets.Length < (movementIds.Count + 2) / 3,
                $"Expected fewer than three-agent batching's 42 packets, got {packets.Length}");
            Assert.Contains(packets, packet => packet.AgentIds.Length > 3);
            AssertSerializedBatchesFitRelay(serializer, network);
            output.WriteLine(
                $"125 incompressible agents: {packets.Length} packets; batch sizes " +
                string.Join(",", packets.Select(packet => packet.AgentIds.Length)));

            ushort[] sentAgents = packets
                .SelectMany(packet => packet.AgentIds)
                .ToArray();
            Assert.Equal(movementIds.Count, sentAgents.Length);
            Assert.Equal(sentAgents.Length, sentAgents.Distinct().Count());
            Assert.All(movementIds, id => Assert.Contains(id, sentAgents));
        });
    }

    [Fact]
    public void PollMovement_NoCommonPayloadBudgetSendsNoPartialSnapshots()
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
            network.MaxUnreliablePayloadBytes = 0;

            Assert.True(registry.TryRegisterAgent(
                "peer", Guid.NewGuid(), 1, SpawnRider(mock)));

            component.AgentMovementHandler.PollMovement(0f);

            Assert.Empty(network.NetworkSentPackets.GetPackets<MovementPacket>());
            Assert.Empty(network.SerializedPacketSends);
        });
    }

    [Fact]
    public void PollMovement_BatchesMountedSnapshotsByActualWireSize()
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
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            network.MaxUnreliablePayloadBytes = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
                serializer,
                "MapEvent_Created_0000",
                "76561198000000042",
                LiteNetP2PClient.SafeSinglePacketBytes);
            var movementIds = new List<ushort>();

            for (int i = 0; i < 48; i++)
            {
                Agent mount = mock.SpawnMount();
                Assert.True(AgentMirror.TryGet(mount, out var mirror));
                PopulateIncompressibleMovementState(mirror, i + 1);
                ushort movementId = (ushort)(i + 1);
                Assert.True(registry.TryRegisterAgent(
                    "peer", Guid.NewGuid(), movementId, mount));
                movementIds.Add(movementId);
            }

            component.AgentMovementHandler.PollMovement(0f);

            MountMovementPacket[] packets = network.NetworkSentPackets
                .GetPackets<MountMovementPacket>()
                .ToArray();
            Assert.NotEmpty(packets);
            Assert.Contains(packets, packet => packet.MountIds.Length > 3);
            AssertSerializedBatchesFitRelay(serializer, network);
            output.WriteLine(
                $"48 incompressible mounts: {packets.Length} packets; batch sizes " +
                string.Join(",", packets.Select(packet => packet.MountIds.Length)));

            ushort[] sentMounts = packets
                .SelectMany(packet => packet.MountIds)
                .ToArray();
            Assert.Equal(movementIds, sentMounts);
        });
    }

    [Fact]
    public void PollMovement_ProbesForBatchGrowthOnlyOncePerScopeAndTick()
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
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            component.AgentMovementHandler.Dispose();
            var compressor = new CountingMovementPacketCompressor(
                new MovementPacketCompressor(serializer));
            using var handler = new AgentMovementHandler(
                network,
                peer.Resolve<IPacketManager>(),
                peer.Resolve<IMessageBroker>(),
                registry,
                peer.Resolve<IControllerIdProvider>(),
                peer.Resolve<IAgentEquipmentApplier>(),
                new MovementBatchSender(network, compressor),
                peer.Resolve<IPuppetMountStateRepairer>());
            network.MaxUnreliablePayloadBytes = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
                serializer,
                "MapEvent_Created_0000",
                "76561198000000042",
                LiteNetP2PClient.SafeSinglePacketBytes);
            var riderMirrors = new List<MirrorAgent>();

            for (int i = 0; i < 18; i++)
            {
                Agent rider = SpawnRider(mock);
                Agent mount = mock.SpawnMount(rider);
                Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
                Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
                PopulateIncompressibleMovementState(riderMirror, i + 1);
                PopulateIncompressibleMovementState(mountMirror, i + 101);
                riderMirrors.Add(riderMirror);
                Assert.True(registry.TryRegisterAgent(
                    "peer", Guid.NewGuid(), (ushort)((i * 2) + 1), rider));
                Assert.True(registry.TryRegisterAgent(
                    "peer", Guid.NewGuid(), (ushort)((i * 2) + 2), mount));
            }

            handler.PollMovement(0f);
            network.NetworkSentPackets.Packets.Clear();
            network.SerializedPacketSends.Clear();
            compressor.Reset();
            foreach (MirrorAgent mirror in riderMirrors)
                mirror.Position += new Vec3(1f, 0f, 0f);

            handler.PollMovement(0.025f);

            MovementPacket[] packets = network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .ToArray();
            Assert.True(packets.Length > 1);
            Assert.InRange(
                compressor.SerializeCalls,
                packets.Length,
                packets.Length + 1);
            AssertSerializedBatchesFitRelay(serializer, network);
        });
    }

    [Fact]
    public void PollMovement_OversizedIdentityScopeBatchesGuidFallback()
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
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            network.MaxUnreliablePayloadBytes = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
                serializer,
                "MapEvent_Created_0000",
                "76561198000000042",
                LiteNetP2PClient.SafeSinglePacketBytes);
            Guid agentId = Guid.NewGuid();
            Guid mountId = Guid.NewGuid();
            var agentIds = new List<Guid> { agentId };
            string oversizedScope = CreateIncompressibleString(3000);
            Agent rider = SpawnRider(mock);
            Agent mount = mock.SpawnMount(rider);

            Assert.True(registry.TryRegisterAgent(
                "peer",
                "original-owner",
                oversizedScope,
                agentId,
                1,
                rider));
            Assert.True(registry.TryRegisterAgent(
                "peer",
                "original-owner",
                oversizedScope,
                mountId,
                2,
                mount));

            for (int i = 0; i < 11; i++)
            {
                Agent additionalRider = SpawnRider(mock);
                Assert.True(AgentMirror.TryGet(additionalRider, out var mirror));
                PopulateIncompressibleMovementState(mirror, i + 1);
                Guid additionalAgentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent(
                    "peer",
                    "original-owner",
                    oversizedScope,
                    additionalAgentId,
                    (ushort)(i + 3),
                    additionalRider));
                agentIds.Add(additionalAgentId);
            }

            component.AgentMovementHandler.PollMovement(0f);

            MovementPacket[] packets = network.NetworkSentPackets
                .GetPackets<MovementPacket>()
                .ToArray();
            Assert.NotEmpty(packets);
            Assert.All(packets, packet =>
            {
                Assert.Null(packet.IdentityScopeId);
                Assert.Null(packet.AgentIds);
            });
            Assert.Contains(packets, packet => packet.AgentGuids.Length > 1);
            Assert.True(packets.Length < (agentIds.Count + 2) / 3,
                $"Expected fewer than three-agent batching's {(agentIds.Count + 2) / 3} packets, " +
                $"got {packets.Length}");
            Assert.Equal(agentIds, packets.SelectMany(packet => packet.AgentGuids));

            MovementPacket mountedPacket = Assert.Single(
                packets,
                packet => packet.AgentGuids.Contains(agentId));
            int mountedAgentIndex = Array.IndexOf(mountedPacket.AgentGuids, agentId);
            AgentMountData mountData = Assert.IsType<AgentMountData>(
                mountedPacket.Agents[mountedAgentIndex].MountData);
            Assert.Equal((ushort)2, mountData.MountMovementId);
            Assert.Null(mountData.MountIdentityScopeId);
            AssertSerializedBatchesFitRelay(serializer, network);
        });
    }

    [Fact]
    public void RelayPayloadBudget_UsesExactSerializedBoundary()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        const string instanceId = "MapEvent_Created_0000";
        const string controllerId = "76561198000000042";
        int ceiling = LiteNetP2PClient.SafeSinglePacketBytes;
        int payloadBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
            serializer,
            instanceId,
            controllerId,
            ceiling);

        byte[] exact = serializer.Serialize(new RelayPacket(
            DeliveryMethod.Unreliable,
            instanceId,
            controllerId,
            new byte[payloadBudget]));
        byte[] oneByteOver = serializer.Serialize(new RelayPacket(
            DeliveryMethod.Unreliable,
            instanceId,
            controllerId,
            new byte[payloadBudget + 1]));

        Assert.Equal(ceiling, exact.Length);
        Assert.True(oneByteOver.Length > ceiling);
    }

    [Fact]
    public void UnreliablePayloadBudget_IgnoresAnUnusableRouteWhenAnotherRouteCanSend()
    {
        const string instanceId = "MapEvent_Created_0000";
        const string usableControllerId = "76561198000000042";
        string unusableControllerId = new string('x', LiteNetP2PClient.SafeSinglePacketBytes);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var compressor = new MovementPacketCompressor(serializer);
        int usableBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
            serializer,
            instanceId,
            usableControllerId,
            LiteNetP2PClient.SafeSinglePacketBytes);
        int unusableBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
            serializer,
            instanceId,
            unusableControllerId,
            LiteNetP2PClient.SafeSinglePacketBytes);
        Assert.True(usableBudget > 0);
        Assert.Equal(0, unusableBudget);

        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.IsTunneled).Returns(true);
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        missionContext.SetupGet(value => value.ControllersInMission)
            .Returns(new[] { unusableControllerId });
        var messageBroker = new Mock<IMessageBroker>();
        var packetManager = new Mock<IPacketManager>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();

        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer,
            messageBroker.Object,
            packetManager.Object,
            controllerIdProvider.Object,
            steamBridge.Object,
            compressor);
        client.ConnectToInstance(instanceId);

        Assert.Equal(0, client.GetMaxUnreliablePayloadBytes());

        missionContext.SetupGet(value => value.ControllersInMission)
            .Returns(new[] { usableControllerId, unusableControllerId });
        Assert.Equal(usableBudget, client.GetMaxUnreliablePayloadBytes());

        client.SendAll(new MovementPacket(
            "scope",
            new ushort[] { 1 },
            new AgentData[1]));

        var relayPacket = Assert.IsType<RelayPacket>(
            Assert.Single(relayNetwork.Invocations).Arguments[0]);
        Assert.Equal(usableControllerId, relayPacket.ControllerId);
        Assert.True(relayPacket.Payload.Length <= usableBudget);
    }

    [Fact]
    public void RelaySend_DropsMovementThatOnlyFitsWithoutRelayFraming()
    {
        const string instanceId = "MapEvent_Created_0000";
        const string controllerId = "76561198000000042";
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var compressor = new MovementPacketCompressor(serializer);
        int relayBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
            serializer,
            instanceId,
            controllerId,
            LiteNetP2PClient.SafeSinglePacketBytes);

        MovementPacket oversizedPacket = default;
        byte[] oversizedPayload = Array.Empty<byte>();
        for (int scopeLength = 700; scopeLength <= 1200; scopeLength++)
        {
            var candidate = new MovementPacket(
                CreateIncompressibleString(scopeLength),
                new ushort[] { 1 },
                new AgentData[1]);
            byte[] payload = compressor.Serialize(candidate);
            if (payload.Length <= relayBudget ||
                payload.Length > LiteNetP2PClient.SafeSinglePacketBytes)
            {
                continue;
            }

            oversizedPacket = candidate;
            oversizedPayload = payload;
            break;
        }

        Assert.NotEmpty(oversizedPayload);
        Assert.InRange(
            oversizedPayload.Length,
            relayBudget + 1,
            LiteNetP2PClient.SafeSinglePacketBytes);

        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.IsTunneled).Returns(true);
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        var messageBroker = new Mock<IMessageBroker>();
        var packetManager = new Mock<IPacketManager>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();

        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer,
            messageBroker.Object,
            packetManager.Object,
            controllerIdProvider.Object,
            steamBridge.Object,
            compressor);
        client.ConnectToInstance(instanceId);

        client.Send(controllerId, oversizedPacket);

        relayNetwork.Verify(
            network => network.SendAll(It.IsAny<IPacket>()),
            Times.Never);

        var safePacket = new MovementPacket(
            "scope",
            new ushort[] { 1 },
            new AgentData[1]);
        byte[] safePayload = compressor.Serialize(safePacket);
        Assert.True(safePayload.Length <= relayBudget);

        client.Send(controllerId, safePacket);

        var relayPacket = Assert.IsType<RelayPacket>(
            Assert.Single(relayNetwork.Invocations).Arguments[0]);
        Assert.Equal(safePayload, relayPacket.Payload);
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
            var movement = Assert.Single(
                network.NetworkSentPackets.GetPackets<MovementPacket>());
            Assert.Equal(new[] { agentId }, movement.AgentGuids);
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

            Assert.True(wire.Length < original.Length,
                $"LZ4 envelope was {wire.Length} bytes for {original.Length} input bytes");
            IPacket envelope = serializer.Deserialize<IPacket>(wire);
            Assert.IsType<CompressedMovementPacket>(envelope);
            Assert.True(compressor.TryRestore(envelope, out var restored));

            var roundTripped = Assert.IsType<MovementPacket>(restored);
            Assert.Equal(movement.IdentityScopeId, roundTripped.IdentityScopeId);
            Assert.Equal(movement.AgentIds, roundTripped.AgentIds);
            Assert.Equal(movement.Agents.Length, roundTripped.Agents.Length);
            Assert.All(
                roundTripped.Agents,
                agent => Assert.Equal(
                    (uint)(Agent.MovementControlFlag.Forward |
                        Agent.MovementControlFlag.TurnLeft),
                    agent.MovementFlag));
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
    public void ThreeMountedSnapshots_FitAndDispatchThroughRelay()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            _ = peer.Resolve<ICoopMissionComponent>();
            var packetManager = peer.Resolve<IPacketManager>();
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
            AssertFitsAndDispatchesThroughRelay(serializer, compressor, packetManager, peer.NetPeer,
                new MovementPacket("76561198000000042", ids, riders));
            AssertFitsAndDispatchesThroughRelay(serializer, compressor, packetManager, peer.NetPeer,
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

    private static void AssertFitsAndDispatchesThroughRelay(
        ProtoBufSerializer serializer,
        MovementPacketCompressor compressor,
        IPacketManager packetManager,
        NetPeer sourcePeer,
        IPacket packet)
    {
        byte[] payload = compressor.Serialize(packet);
        Assert.IsType<CompressedMovementPacket>(
            serializer.Deserialize<IPacket>(payload));
        Assert.True(payload.Length <= LiteNetP2PClient.SafeSinglePacketBytes,
            $"Direct payload was {payload.Length} bytes");

        byte[] relayBytes = serializer.Serialize(new RelayPacket(
            packet.DeliveryMethod,
            "MapEvent_Created_0000",
            "76561198000000042",
            payload));
        Assert.True(relayBytes.Length <= LiteNetP2PClient.SafeSinglePacketBytes,
            $"Relay payload was {relayBytes.Length} bytes");

        var relay = Assert.IsType<RelayPacket>(
            serializer.Deserialize<IPacket>(relayBytes));
        IPacket received = serializer.Deserialize<IPacket>(relay.Payload);

        var restoredHandler = new RecordingPacketHandler(packet.PacketType);
        packetManager.RegisterPacketHandler(restoredHandler);

        try
        {
            packetManager.HandleReceive(sourcePeer, received);

            Assert.Equal(1, restoredHandler.HandleCount);
            Assert.Same(sourcePeer, restoredHandler.SourcePeer);
            Assert.Equal(packet.GetType(), restoredHandler.Received.GetType());
        }
        finally
        {
            packetManager.RemovePacketHandler(restoredHandler);
        }
    }

    private static Agent SpawnRider(MockMission mock)
    {
        return mock.SpawnAgent(
            new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
    }

    private static void AssertSerializedBatchesFitRelay(
        ProtoBufSerializer serializer,
        MockBattleNetwork network)
    {
        Assert.NotEmpty(network.SerializedPacketSends);
        foreach (SerializedPacketSend send in network.SerializedPacketSends)
        {
            Assert.True(send.Payload.Length <= network.MaxUnreliablePayloadBytes,
                $"Direct payload was {send.Payload.Length} bytes");

            byte[] relay = serializer.Serialize(new RelayPacket(
                send.Packet.DeliveryMethod,
                "MapEvent_Created_0000",
                "76561198000000042",
                send.Payload));
            Assert.True(relay.Length <= LiteNetP2PClient.SafeSinglePacketBytes,
                $"Relay payload was {relay.Length} bytes");
        }
    }

    private static void PopulateIncompressibleMovementState(MirrorAgent mirror, int seed)
    {
        uint state = unchecked((uint)(seed * 747796405) + 2891336453u);
        mirror.Position = new Vec3(
            NextFloat(ref state, 10000f),
            NextFloat(ref state, 10000f),
            NextFloat(ref state, 10000f));
        mirror.InputVector = new Vec2(
            NextFloat(ref state, 1f),
            NextFloat(ref state, 1f));
        mirror.LookDirection = new Vec3(
            NextFloat(ref state, 1f),
            NextFloat(ref state, 1f),
            NextFloat(ref state, 1f));
        mirror.MovementDirection = new Vec2(
            NextFloat(ref state, 1f),
            NextFloat(ref state, 1f));
        mirror.RealGlobalVelocity = new Vec3(
            NextFloat(ref state, 100f),
            NextFloat(ref state, 100f),
            NextFloat(ref state, 100f));
        mirror.Action0Flags = (AnimFlags)Next(ref state);
        mirror.Action1Flags = (AnimFlags)Next(ref state);
        mirror.Action0Progress = NextFloat(ref state, 1f);
        mirror.Action1Progress = NextFloat(ref state, 1f);
        mirror.Action0Index = unchecked((int)Next(ref state));
        mirror.Action1Index = unchecked((int)Next(ref state));
    }

    private static float NextFloat(ref uint state, float magnitude)
    {
        return (((Next(ref state) / (float)uint.MaxValue) * 2f) - 1f) * magnitude;
    }

    private static uint Next(ref uint state)
    {
        state = unchecked((state * 1664525u) + 1013904223u);
        return state;
    }

    private static string CreateIncompressibleString(int length)
    {
        var characters = new char[length];
        uint state = 0x9E3779B9u;
        for (int i = 0; i < characters.Length; i++)
            characters[i] = (char)('!' + (Next(ref state) % 94));
        return new string(characters);
    }

    private sealed class CountingMovementPacketCompressor : IMovementPacketCompressor
    {
        private readonly IMovementPacketCompressor inner;

        public int SerializeCalls { get; private set; }

        public CountingMovementPacketCompressor(IMovementPacketCompressor inner)
        {
            this.inner = inner;
        }

        public byte[] Serialize(IPacket packet)
        {
            SerializeCalls++;
            return inner.Serialize(packet);
        }

        public bool TryRestore(IPacket packet, out IPacket restored)
        {
            return inner.TryRestore(packet, out restored);
        }

        public void Reset()
        {
            SerializeCalls = 0;
        }
    }

    private static void PopulateMovementState(MirrorAgent mirror, int multiplier)
    {
        mirror.Position = new Vec3(1024.25f * multiplier, -2048.5f * multiplier, 512.75f * multiplier);
        mirror.InputVector = new Vec2(0.75f, -0.5f);
        mirror.LookDirection = new Vec3(-0.25f, 0.5f, 0.75f);
        mirror.MovementDirection = new Vec2(-0.75f, 0.5f);
        mirror.MovementFlags =
            Agent.MovementControlFlag.Forward |
            Agent.MovementControlFlag.TurnLeft |
            Agent.MovementControlFlag.DefendUp;
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

    private sealed class RecordingPacketHandler : IPacketHandler
    {
        public PacketType PacketType { get; }
        public int HandleCount { get; private set; }
        public NetPeer SourcePeer { get; private set; } = null!;
        public IPacket Received { get; private set; } = null!;

        public RecordingPacketHandler(PacketType packetType)
        {
            PacketType = packetType;
        }

        public void Dispose()
        {
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            HandleCount++;
            SourcePeer = peer;
            Received = packet;
        }
    }
}
