using Common.BattleBenchmark;
using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Surrogates;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>Covers how many times mission message sends serialize, and the bytes each peer receives.</summary>
public class LiteNetP2PClientBroadcastTests
{
    private static readonly string[] Controllers = { "peer-a", "peer-b", "peer-c" };

    public LiteNetP2PClientBroadcastTests()
    {
        _ = new SurrogateCollection();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void SendAll_SerializesOnceAndEachPeerGetsThePerPeerBytes(int peerCount)
    {
        using var fixture = new Fixture(Controllers.Take(peerCount).ToArray());
        NetworkBattleAgentDied message = CreateDeath(1);

        fixture.Client.SendAll(message);
        fixture.Client.FlushPendingMessages();

        Assert.Equal(peerCount == 0 ? 0 : 1, fixture.CountingSerializer.SerializeCalls);
        byte[] expected = MessagePacket.Create(message, fixture.PlainSerializer).Data;
        Assert.Equal(Controllers.Take(peerCount), fixture.RelayedTargets());
        Assert.All(fixture.Relayed, packet =>
        {
            Assert.Equal(DeliveryMethod.ReliableOrdered, packet.DeliveryMethod);
            Assert.Equal(expected, packet.Payload);
        });
    }

    [Theory]
    [InlineData("SendAllBut")]
    [InlineData("Send")]
    public void SingleMessageSends_SerializeOnce(string sendPath)
    {
        using var fixture = new Fixture(Controllers);
        NetworkBattleAgentDied message = CreateDeath(2);

        if (sendPath == "SendAllBut")
            fixture.Client.SendAllBut(Controllers[0], message);
        else
            fixture.Client.Send(Controllers[0], message);
        fixture.Client.FlushPendingMessages();

        Assert.Equal(1, fixture.CountingSerializer.SerializeCalls);
        string[] expectedTargets = sendPath == "SendAllBut" ? Controllers.Skip(1).ToArray() : new[] { Controllers[0] };
        Assert.Equal(expectedTargets, fixture.RelayedTargets());
        byte[] expected = MessagePacket.Create(message, fixture.PlainSerializer).Data;
        Assert.All(fixture.Relayed, packet => Assert.Equal(expected, packet.Payload));
    }

    [Fact]
    public void SendAll_BatchedPayloadsStayUnchangedAndEachPeerGetsItsOwnEnvelope()
    {
        using var fixture = new Fixture(Controllers);
        NetworkBattleAgentDied[] messages = { CreateDeath(3), CreateDeath(4), CreateDeath(5) };
        byte[][] expected = messages
            .Select(message => MessagePacket.Create(message, fixture.PlainSerializer).Data)
            .ToArray();

        foreach (NetworkBattleAgentDied message in messages)
            fixture.Client.SendAll(message);
        fixture.Client.FlushPendingMessages();

        Assert.Equal(messages.Length, fixture.CountingSerializer.SerializeCalls);
        Assert.Equal(Controllers, fixture.RelayedTargets());
        // Each peer's wire buffer is its own array even though the payloads inside are shared.
        Assert.Equal(Controllers.Length, fixture.Relayed.Select(packet => packet.Payload).Distinct().Count());
        foreach (RelayPacket packet in fixture.Relayed)
        {
            var aggregate = Assert.IsType<AggregateMessagePacket>(fixture.PlainSerializer.Deserialize(packet.Payload));
            Assert.Equal(expected, aggregate.Messages);
        }

        // Writing into one peer's wire buffer leaves the other peers' bytes as they were.
        Array.Clear(fixture.Relayed[0].Payload, 0, fixture.Relayed[0].Payload.Length);
        foreach (RelayPacket packet in fixture.Relayed.Skip(1))
        {
            var aggregate = Assert.IsType<AggregateMessagePacket>(fixture.PlainSerializer.Deserialize(packet.Payload));
            Assert.Equal(expected, aggregate.Messages);
        }
    }

    [Fact]
    public void SendAll_RecordingDoesNotSerializeTheRelayAgain()
    {
        var recorder = new Mock<IBattleBenchmarkRecorder>();
        recorder.SetupGet(value => value.IsRecording).Returns(true);
        using var fixture = new Fixture(Controllers, recorder.Object);

        fixture.Client.SendAll(CreateDeath(6));
        fixture.Client.FlushPendingMessages();

        Assert.Equal(1, fixture.CountingSerializer.SerializeCalls);
        Assert.Equal(Controllers, fixture.RelayedTargets());
    }

    private static NetworkBattleAgentDied CreateDeath(int value)
    {
        return new NetworkBattleAgentDied(
            new Guid(value, 0, 0, new byte[8]),
            wounded: false,
            Guid.Empty,
            inflictedDamage: value,
            BoneBodyPartType.Head,
            deathAction: value);
    }

    /// <summary>Counts <see cref="Serialize"/> calls and forwards everything to a real serializer.</summary>
    private sealed class CountingSerializer : ICommonSerializer
    {
        private readonly ICommonSerializer inner;

        public CountingSerializer(ICommonSerializer inner)
        {
            this.inner = inner;
        }

        public int SerializeCalls { get; set; }

        public T Deserialize<T>(byte[] data) => inner.Deserialize<T>(data);

        public object Deserialize(byte[] data) => inner.Deserialize(data);

        public byte[] Serialize(object obj)
        {
            SerializeCalls++;
            return inner.Serialize(obj);
        }
    }

    /// <summary>A mission client whose controllers all route through the relay, so every send is captured.</summary>
    private sealed class Fixture : IDisposable
    {
        public readonly ProtoBufSerializer PlainSerializer = new(new SerializableTypeMapper());
        public readonly CountingSerializer CountingSerializer;
        public readonly List<RelayPacket> Relayed = new();
        public readonly LiteNetP2PClient Client;

        public Fixture(string[] controllers, IBattleBenchmarkRecorder? battleBenchmark = null)
        {
            CountingSerializer = new CountingSerializer(PlainSerializer);
            var config = new Mock<INetworkConfig>();
            config.SetupGet(value => value.IsTunneled).Returns(true);
            var missionContext = new Mock<IMissionContext>();
            missionContext.SetupGet(value => value.ControllersInMission).Returns(controllers);
            var relayNetwork = new Mock<IRelayNetwork>();
            relayNetwork
                .Setup(network => network.SendAll(It.IsAny<IPacket>()))
                .Callback<IPacket>(packet => Relayed.Add(Assert.IsType<RelayPacket>(packet)));

            // Aggregate envelopes use the plain serializer, so the count only covers message serialization.
            Client = new LiteNetP2PClient(
                config.Object,
                relayNetwork.Object,
                missionContext.Object,
                CountingSerializer,
                Mock.Of<IMessageBroker>(),
                Mock.Of<IPacketManager>(),
                Mock.Of<IMessagePacketHandler>(),
                Mock.Of<IControllerIdProvider>(),
                Mock.Of<ISteamMissionBridge>(),
                new MovementPacketCompressor(CountingSerializer),
                new ReliableMessageBatcher<string>(PlainSerializer),
                () => new Common.Logging.ReceivePathDiagnostics(),
                battleBenchmark);
            Client.ConnectToInstance("broadcast-test");

            // Each controller's relay budget is probed once with the same serializer, so fill them before counting.
            Client.GetMaxUnreliablePayloadBytes();
            CountingSerializer.SerializeCalls = 0;
        }

        // Flushing walks every peer's buffer in no set order, so only the order within one peer is fixed.
        public string[] RelayedTargets() => Relayed.Select(packet => packet.ControllerId).OrderBy(id => id).ToArray();

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
