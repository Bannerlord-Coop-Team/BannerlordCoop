using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Extensions;
using GameInterface.Services.Entity;
using GameInterface.Surrogates;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class BattleSpawnSendOrderingTests
{
    private const string FirstController = "spawn-peer-a";
    private const string SecondController = "spawn-peer-b";
    private const string ExcludedController = "spawn-peer-c";

    public BattleSpawnSendOrderingTests()
    {
        _ = new SurrogateCollection();
    }

    [Theory]
    [InlineData("message")]
    [InlineData("all")]
    [InlineData("all-but")]
    [InlineData("packet")]
    [InlineData("serialized-packet")]
    public void SpawnSend_FlushesEarlierMessagesAndSendsBarePayloadBeforeLaterMessages(string sendPath)
    {
        using var fixture = new SendFixture();
        string[] recipients = sendPath switch
        {
            "all" => new[] { FirstController, SecondController, ExcludedController },
            "all-but" => new[] { FirstController, SecondController },
            _ => new[] { FirstController },
        };
        var earlier = new[] { CreateDeath(), CreateDeath() };
        foreach (string recipient in recipients)
        {
            foreach (IMessage message in earlier)
                fixture.Client.Send(recipient, message);
        }
        Assert.Empty(fixture.RelayedPackets);

        var codec = new BattleAgentSpawnBatchCodec();
        NetworkSpawnBattleAgents spawn = CreateSpawn(codec);
        Assert.True(fixture.Serializer.Serialize(spawn).Length < ReliableMessageBatcher<string>.DefaultBudgetBytes);

        switch (sendPath)
        {
            case "message":
                fixture.Client.Send(FirstController, spawn);
                break;
            case "all":
                fixture.Client.SendAll(spawn);
                break;
            case "all-but":
                fixture.Client.SendAllBut(ExcludedController, spawn);
                break;
            case "packet":
                fixture.Client.Send(FirstController, MessagePacket.Create(spawn, fixture.Serializer));
                break;
            case "serialized-packet":
                MessagePacket packet = MessagePacket.Create(spawn, fixture.Serializer);
                fixture.Client.Send(FirstController, packet, packet.Data);
                break;
        }

        Assert.Equal(recipients.Length * 2, fixture.RelayedPackets.Count);
        foreach (string recipient in recipients)
        {
            RelayPacket[] packets = fixture.RelayedPackets.Where(packet => packet.ControllerId == recipient).ToArray();
            Assert.Equal(2, packets.Length);
            AssertDeaths(fixture.Serializer, packets[0], earlier);
            var receivedSpawn = Assert.IsType<NetworkSpawnBattleAgents>(fixture.Serializer.Deserialize(packets[1].Payload));
            Assert.Null(receivedSpawn.Agents);
            Assert.True(codec.TryDecode(receivedSpawn, out BattleAgentSpawnData[] agents));
            Assert.Equal(Assert.Single(spawn.Agents).AgentId, Assert.Single(agents).AgentId);
            Assert.Equal(spawn.TransferId, receivedSpawn.TransferId);
            Assert.Equal(spawn.Purpose, receivedSpawn.Purpose);
        }

        var later = new[] { CreateDeath(), CreateDeath() };
        foreach (string recipient in recipients)
        {
            foreach (IMessage message in later)
                fixture.Client.Send(recipient, message);
        }
        Assert.Equal(recipients.Length * 2, fixture.RelayedPackets.Count);

        fixture.Client.FlushPendingMessages();

        Assert.Equal(recipients.Length * 3, fixture.RelayedPackets.Count);
        Assert.All(fixture.RelayedPackets, packet =>
        {
            Assert.Contains(packet.ControllerId, recipients);
            Assert.Equal(DeliveryMethod.ReliableOrdered, packet.DeliveryMethod);
            Assert.Equal("spawn-ordering-battle", packet.InstanceId);
        });
        foreach (string recipient in recipients)
        {
            RelayPacket last = fixture.RelayedPackets.Last(packet => packet.ControllerId == recipient);
            AssertDeaths(fixture.Serializer, last, later);
        }
    }

    [Fact]
    public void DirectSpawnSend_QueuesEarlierBatchAndSpawnImmediatelyWhileLaterMessagesWait()
    {
        NetPeer peer = NetPeerExtensions.CreatePeer(97);
        using var fixture = new SendFixture(peer);
        fixture.Client.Send(FirstController, CreateDeath());
        fixture.Client.Send(FirstController, CreateDeath());
        Assert.Equal(0, peer.GetPacketsCountInReliableQueue(0, ordered: true));

        fixture.Client.Send(FirstController, CreateSpawn(new BattleAgentSpawnBatchCodec()));

        Assert.Equal(2, peer.GetPacketsCountInReliableQueue(0, ordered: true));
        fixture.Client.Send(FirstController, CreateDeath());
        fixture.Client.Send(FirstController, CreateDeath());
        Assert.Equal(2, peer.GetPacketsCountInReliableQueue(0, ordered: true));

        fixture.Client.FlushPendingMessages();

        Assert.Equal(3, peer.GetPacketsCountInReliableQueue(0, ordered: true));
        Assert.Empty(fixture.RelayedPackets);
    }

    private static void AssertDeaths(ProtoBufSerializer serializer, RelayPacket packet, NetworkBattleAgentDied[] expected)
    {
        var aggregate = Assert.IsType<AggregateMessagePacket>(serializer.Deserialize(packet.Payload));
        Guid[] received = aggregate.Messages
            .Select(payload => Assert.IsType<NetworkBattleAgentDied>(serializer.Deserialize(payload)).AgentId)
            .ToArray();
        Assert.Equal(expected.Select(message => message.AgentId), received);
    }

    private static NetworkBattleAgentDied CreateDeath()
    {
        return new NetworkBattleAgentDied(Guid.NewGuid(), false, Guid.Empty, 10, BoneBodyPartType.Head, 1);
    }

    private static NetworkSpawnBattleAgents CreateSpawn(BattleAgentSpawnBatchCodec codec)
    {
        var agent = new BattleAgentSpawnData(
            Guid.NewGuid(), "imperial_infantry", new Vec3(1f, 2f, 0f), BattleSideEnum.Attacker,
            100f, "spawn-owner", "map_event_party", 1, default(Equipment), default(BodyProperties),
            missionEquipmentData: null, movementId: 1, movementScopeId: "spawn-owner:scope", authorityRevision: 4);
        return Assert.Single(codec.Encode(new[] { agent }, SpawnBatchPurpose.CatchUp));
    }

    private sealed class SendFixture : IDisposable
    {
        public ProtoBufSerializer Serializer { get; } = new ProtoBufSerializer(new SerializableTypeMapper());
        public List<RelayPacket> RelayedPackets { get; } = new List<RelayPacket>();
        public LiteNetP2PClient Client { get; }

        public SendFixture(NetPeer? peer = null)
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(value => value.IsTunneled).Returns(peer == null);
            var relay = new Mock<IRelayNetwork>();
            relay.Setup(network => network.SendAll(It.IsAny<IPacket>()))
                .Callback<IPacket>(packet => RelayedPackets.Add(Assert.IsType<RelayPacket>(packet)));
            var context = new Mock<IMissionContext>();
            context.SetupGet(value => value.ControllersInMission)
                .Returns(new[] { FirstController, SecondController, ExcludedController });
            if (peer != null)
                context.Setup(value => value.TryGetPeer(FirstController, out peer)).Returns(true);

            Client = new LiteNetP2PClient(
                config.Object, relay.Object, context.Object, Serializer,
                new Mock<IMessageBroker>().Object, new Mock<IPacketManager>().Object,
                new Mock<IMessagePacketHandler>().Object, new Mock<IControllerIdProvider>().Object,
                new Mock<ISteamMissionBridge>().Object, new MovementPacketCompressor(Serializer),
                new ReliableMessageBatcher<string>(Serializer), () => new ReceivePathDiagnostics());
            Client.ConnectToInstance("spawn-ordering-battle");
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
