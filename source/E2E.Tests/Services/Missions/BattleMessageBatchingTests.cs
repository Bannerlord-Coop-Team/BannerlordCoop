using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Extensions;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for battle message wire volume and stale hit presentation.</summary>
public class BattleMessageBatchingTests : MissionTestEnvironment
{
    public BattleMessageBatchingTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void HighVolumeSmallBattleMessages_FitWithinOneReliableWindowAndPreserveDeathOrder()
    {
        const string instanceId = "battle-batching-test";
        const string controllerId = "battle-peer";
        const int logicalMessageCount = 256;

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.IsTunneled).Returns(true);
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        missionContext.SetupGet(value => value.ControllersInMission)
            .Returns(new[] { controllerId });
        var messageBroker = new Mock<IMessageBroker>();
        var packetManager = new Mock<IPacketManager>();
        var messagePacketHandler = new Mock<IMessagePacketHandler>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();
        var compressor = new MovementPacketCompressor(serializer);
        var damageDataMapper = new BattleDamageDataMapper();
        var relayedPackets = new List<RelayPacket>();
        relayNetwork
            .Setup(network => network.SendAll(It.IsAny<IPacket>()))
            .Callback<IPacket>(packet => relayedPackets.Add(Assert.IsType<RelayPacket>(packet)));

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
            new ReliableMessageBatcher<string>(serializer));
        client.ConnectToInstance(instanceId);

        var logicalPayloads = new List<byte[]>(logicalMessageCount);
        for (int i = 0; i < logicalMessageCount; i++)
        {
            IMessage message = CreateBattleMessage(i, damageDataMapper);
            logicalPayloads.Add(MessagePacket.Create(message, serializer).Data);
            client.Send(controllerId, message);
        }

        Assert.True(
            logicalPayloads.Max(payload => payload.Length) < ReliableMessageBatcher<string>.DefaultBudgetBytes,
            $"payload sizes: min={logicalPayloads.Min(payload => payload.Length)} " +
            $"max={logicalPayloads.Max(payload => payload.Length)} " +
            $"average={logicalPayloads.Average(payload => payload.Length):F1}");
        Assert.True(relayedPackets.Count < logicalMessageCount);

        client.FlushPendingMessages();

        Assert.InRange(relayedPackets.Count, 1, 63);
        Assert.All(
            relayedPackets,
            packet => Assert.Equal(LiteNetLib.DeliveryMethod.ReliableOrdered, packet.DeliveryMethod));

        var receivedPayloads = new List<byte[]>(logicalMessageCount);
        foreach (RelayPacket relayPacket in relayedPackets)
        {
            object received = serializer.Deserialize(relayPacket.Payload);
            if (received is AggregateMessagePacket aggregate)
                receivedPayloads.AddRange(aggregate.Messages);
            else
                receivedPayloads.Add(relayPacket.Payload);
        }

        Assert.Equal(logicalPayloads.Count, receivedPayloads.Count);
        for (int i = 0; i < logicalPayloads.Count; i++)
        {
            Assert.Equal(logicalPayloads[i], receivedPayloads[i]);
        }
    }

    [Fact]
    public void DirectRoute_BatchesMessagesIntoOneReliableQueueSlot()
    {
        const string controllerId = "direct-peer";
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var config = new Mock<INetworkConfig>();
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        NetPeer peer = NetPeerExtensions.CreatePeer(98);
        missionContext.SetupGet(value => value.ControllersInMission)
            .Returns(new[] { controllerId });
        missionContext
            .Setup(value => value.TryGetPeer(controllerId, out peer))
            .Returns(true);
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
            new MovementPacketCompressor(serializer),
            new ReliableMessageBatcher<string>(serializer));

        client.Send(controllerId, CreateDeath(CreateGuid(10), 10));
        client.Send(controllerId, CreateDeath(CreateGuid(11), 11));
        Assert.Equal(0, peer.GetPacketsCountInReliableQueue(0, ordered: true));

        client.FlushPendingMessages();

        Assert.Equal(1, peer.GetPacketsCountInReliableQueue(0, ordered: true));
        relayNetwork.Verify(network => network.SendAll(It.IsAny<IPacket>()), Times.Never);
    }

    [Fact]
    public void MissionReceive_PublishesBareAndAggregateMessagesInOrder()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.IsTunneled).Returns(true);
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        var messageBroker = new MessageBroker();
        var packetManager = new PacketManager();
        using var messagePacketHandler = new MessagePacketHandler(
            messageBroker,
            packetManager,
            serializer);
        using var aggregateHandler = new AggregateMessagePacketHandler(
            messagePacketHandler,
            packetManager,
            serializer);
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();
        var received = new List<Guid>();
        Action<MessagePayload<NetworkBattleAgentDied>> subscription =
            payload => received.Add(payload.What.AgentId);
        messageBroker.Subscribe(subscription);

        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer,
            messageBroker,
            packetManager,
            messagePacketHandler,
            controllerIdProvider.Object,
            steamBridge.Object,
            new MovementPacketCompressor(serializer),
            new ReliableMessageBatcher<string>(serializer));
        NetPeer peer = NetPeerExtensions.CreatePeer(99);
        Guid first = CreateGuid(1);
        Guid second = CreateGuid(2);
        Guid third = CreateGuid(3);
        byte[] firstPayload = MessagePacket.Create(CreateDeath(first, 1), serializer).Data;
        byte[] secondPayload = MessagePacket.Create(CreateDeath(second, 2), serializer).Data;
        byte[] thirdPayload = MessagePacket.Create(CreateDeath(third, 3), serializer).Data;

        client.HandleReceivedPayload(peer, firstPayload);
        client.HandleReceivedPayload(
            peer,
            serializer.Serialize(new AggregateMessagePacket(
                new[] { secondPayload, thirdPayload })));

        Assert.Equal(new[] { first, second, third }, received);
        messageBroker.Unsubscribe(subscription);
    }

    [Fact]
    public void PlayBlood_InactiveAgent_DoesNotCallNativePresentation()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            MockMission mission = fixture.CreateMission(client);
            Agent victim = mission.SpawnMount();
            Assert.True(AgentMirror.TryGet(victim, out MirrorAgent mirror));
            mirror.IsActive = false;

            CombatHitPresentationHandler.PlayBlood(victim, 1, 1f);

            Assert.Equal(0, mirror.BloodBurstCalls);

            mirror.IsActive = true;
            CombatHitPresentationHandler.PlayBlood(victim, 1, 1f);

            Assert.Equal(1, mirror.BloodBurstCalls);
        });
    }

    private static IMessage CreateBattleMessage(int index, IBattleDamageDataMapper damageDataMapper)
    {
        Guid victimId = CreateGuid(index);
        Guid attackerId = CreateGuid(index + 10_000);
        switch (index % 3)
        {
            case 0:
                var blow = new Blow(index)
                {
                    InflictedDamage = (index % 100) + 1,
                    DamageType = DamageTypes.Pierce,
                    GlobalPosition = new Vec3(index, index + 1, index + 2),
                };
                var collisionData = new AttackCollisionData
                {
                    InflictedDamage = blow.InflictedDamage,
                };
                BattleDamageData damageData = damageDataMapper.Pack(in blow, in collisionData);
                return new NetworkApplyBattleDamage(
                    victimId,
                    attackerId,
                    damageData,
                    blow.IsMissile);
            case 1:
                return new NetworkMeleeHitPresentation(
                    victimId,
                    isMount: false,
                    MeleeHitPresentationKind.Blood,
                    collisionBoneIndex: index,
                    new Vec3(index, 0f, 0f),
                    WeaponClass.OneHandedSword,
                    physicsMaterialIndex: -1,
                    strength: 1f);
            default:
                return new NetworkBattleAgentDied(
                    victimId,
                    wounded: false,
                    attackerId,
                    inflictedDamage: (index % 100) + 1,
                    BoneBodyPartType.Head,
                    deathAction: index);
        }
    }

    private static NetworkBattleAgentDied CreateDeath(Guid victimId, int deathAction)
    {
        return new NetworkBattleAgentDied(
            victimId,
            wounded: false,
            Guid.Empty,
            inflictedDamage: 1,
            BoneBodyPartType.Head,
            deathAction);
    }

    private static Guid CreateGuid(int value)
    {
        return new Guid(value, 0, 0, new byte[8]);
    }
}
