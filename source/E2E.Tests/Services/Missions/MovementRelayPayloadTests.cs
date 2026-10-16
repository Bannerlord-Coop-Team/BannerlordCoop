using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Surrogates;
using LiteNetLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Services.Network;
using Moq;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

/// <summary>Relay payload budgets, compression and delivery selection for movement on a mocked P2P client; needs no game instances.</summary>
public class MovementRelayPayloadTests
{
    public MovementRelayPayloadTests()
    {
        _ = new SurrogateCollection();
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
        var messagePacketHandler = new Mock<IMessagePacketHandler>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();

        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer,
            messageBroker.Object,
            packetManager.Object,
            messagePacketHandler.Object,
            controllerIdProvider.Object,
            steamBridge.Object,
            compressor,
            new ReliableMessageBatcher<string>(serializer), () => new Common.Logging.ReceivePathDiagnostics());
        client.ConnectToInstance(instanceId);

        Assert.Equal(0, client.GetMaxUnreliablePayloadBytes());
        Assert.Equal(0, client.GetMaxUnreliablePayloadBytes(unusableControllerId));

        missionContext.SetupGet(value => value.ControllersInMission)
            .Returns(new[] { usableControllerId, unusableControllerId });
        Assert.Equal(usableBudget, client.GetMaxUnreliablePayloadBytes());
        Assert.Equal(usableBudget, client.GetMaxUnreliablePayloadBytes(usableControllerId));
        Assert.Equal(0, client.GetMaxUnreliablePayloadBytes(unusableControllerId));

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
                MovementTrafficTests.CreateIncompressibleString(scopeLength),
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
        var messagePacketHandler = new Mock<IMessagePacketHandler>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();

        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer,
            messageBroker.Object,
            packetManager.Object,
            messagePacketHandler.Object,
            controllerIdProvider.Object,
            steamBridge.Object,
            compressor,
            new ReliableMessageBatcher<string>(serializer), () => new Common.Logging.ReceivePathDiagnostics());
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
    public void Lz4MovementCompression_RejectsCorruptEnvelope()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var compressor = new MovementPacketCompressor(serializer);
        var corrupt = new CompressedMovementPacket(512, new byte[] { 1, 2, 3 });

        Assert.False(compressor.TryRestore(corrupt, out _));
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
    [Fact]
    public void SerializerEquivalence_RelayBudgetMatchesLegacy()
    {
        var mapper = new SerializableTypeMapper();
        var legacy = new LegacyProtoBufSerializer(mapper);
        var current = new ProtoBufSerializer(mapper);

        foreach (int ceiling in new[] { 0, 16, 127, 128, 300, LiteNetP2PClient.SafeSinglePacketBytes, 20_000 })
        {
            Assert.Equal(
                LiteNetP2PClient.CalculateMaxRelayPayloadBytes(legacy, "MapEvent_Created_0000", "76561198000000042", ceiling),
                LiteNetP2PClient.CalculateMaxRelayPayloadBytes(current, "MapEvent_Created_0000", "76561198000000042", ceiling));
        }
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
