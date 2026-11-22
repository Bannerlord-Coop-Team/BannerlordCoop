using Common.BattleBenchmark;
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
using System.Diagnostics;
using System.Net;
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

        MovementPacket oversizedPacket = CreateMovementOverRelayBudget(compressor, relayBudget);

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

    [Fact]
    public void SentTypeCount_LeavesOutMovementThatNeverLeaves()
    {
        const string instanceId = "MapEvent_Created_0000";
        const string directControllerId = "76561198000000041";
        const string relayControllerId = "76561198000000042";
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var compressor = new MovementPacketCompressor(serializer);
        var typeSends = new List<(Type Type, int Bytes)>();
        var routeSends = new List<(string Route, int Bytes)>();
        var recorder = new Mock<IBattleBenchmarkRecorder>();
        recorder.SetupGet(value => value.IsRecording).Returns(true);
        recorder.Setup(value => value.RecordTypeSent(It.IsAny<Type>(), It.IsAny<int>()))
            .Callback<Type, int>((type, bytes) => typeSends.Add((type, bytes)));
        recorder.Setup(value => value.RecordSent(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Callback<string, bool, int, int, bool>((route, _, bytes, _, _) => routeSends.Add((route, bytes)));

        var serverListener = new EventBasedNetListener();
        serverListener.ConnectionRequestEvent += request => request.AcceptIfKey("sent-type-test");
        var serverTransport = new NetManager(serverListener);
        var clientTransport = new NetManager(new EventBasedNetListener());
        try
        {
            Assert.True(serverTransport.StartInManualMode(IPAddress.Loopback, IPAddress.IPv6Loopback, 0));
            Assert.True(clientTransport.StartInManualMode(IPAddress.Loopback, IPAddress.IPv6Loopback, 0));
            NetPeer directPeer = clientTransport.Connect(IPAddress.Loopback.ToString(), serverTransport.LocalPort, "sent-type-test");
            var timer = Stopwatch.StartNew();
            while (directPeer.ConnectionState != LiteNetLib.ConnectionState.Connected && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                serverTransport.ManualUpdate(15);
                clientTransport.ManualUpdate(15);
                serverTransport.PollEvents();
                clientTransport.PollEvents();
                Thread.Sleep(1);
            }
            Assert.Equal(LiteNetLib.ConnectionState.Connected, directPeer.ConnectionState);

            var config = new Mock<INetworkConfig>();
            config.SetupGet(value => value.IsTunneled).Returns(true);
            var relayNetwork = new Mock<IRelayNetwork>();
            var missionContext = new Mock<IMissionContext>();
            missionContext.Setup(value => value.TryGetPeer(directControllerId, out directPeer)).Returns(true);
            using var client = new LiteNetP2PClient(
                config.Object,
                relayNetwork.Object,
                missionContext.Object,
                serializer,
                Mock.Of<IMessageBroker>(),
                Mock.Of<IPacketManager>(),
                Mock.Of<IMessagePacketHandler>(),
                Mock.Of<IControllerIdProvider>(),
                Mock.Of<ISteamMissionBridge>(),
                compressor,
                new ReliableMessageBatcher<string>(serializer), () => new Common.Logging.ReceivePathDiagnostics(),
                recorder.Object);
            client.ConnectToInstance(instanceId);
            int relayBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
                serializer, instanceId, relayControllerId, LiteNetP2PClient.SafeSinglePacketBytes);
            var movement = new MovementPacket("scope", new ushort[] { 1 }, new AgentData[1]);
            var overDatagram = new MovementPacket(
                MovementTrafficTests.CreateIncompressibleString(LiteNetP2PClient.SafeSinglePacketBytes + 100),
                new ushort[] { 1 },
                new AgentData[1]);

            client.Send(directControllerId, movement);
            client.Send(directControllerId, overDatagram);
            client.Send(relayControllerId, CreateMovementOverRelayBudget(compressor, relayBudget));
            client.Send(relayControllerId, movement);

            // Only the two copies that left count: the direct one on its route, the relayed one inside its relay packet.
            int movementBytes = compressor.Serialize(movement).Length;
            Assert.Equal(new[] { (typeof(MovementPacket), movementBytes), (typeof(MovementPacket), movementBytes) }, typeSends);
            Assert.Equal(new[] { (directControllerId, movementBytes) }, routeSends);
            var relayed = Assert.IsType<RelayPacket>(Assert.Single(relayNetwork.Invocations).Arguments[0]);
            Assert.Equal(movementBytes, relayed.Payload.Length);
        }
        finally
        {
            clientTransport.Stop();
            serverTransport.Stop();
        }
    }

    // A movement packet that fits one datagram but not the relay payload budget, so only the relay drops it.
    private static MovementPacket CreateMovementOverRelayBudget(MovementPacketCompressor compressor, int relayBudget)
    {
        for (int scopeLength = 700; scopeLength <= 1200; scopeLength++)
        {
            var candidate = new MovementPacket(
                MovementTrafficTests.CreateIncompressibleString(scopeLength),
                new ushort[] { 1 },
                new AgentData[1]);
            int length = compressor.Serialize(candidate).Length;
            if (length > relayBudget && length <= LiteNetP2PClient.SafeSinglePacketBytes) return candidate;
        }

        throw new InvalidOperationException("No movement packet fits a datagram but not the relay budget.");
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
