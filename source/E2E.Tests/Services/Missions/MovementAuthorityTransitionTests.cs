using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.PacketHandlers;
using E2E.Tests.Environment.Extensions;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions;
using Missions.Agents;
using Moq;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Messages;
using Missions.Services.Network;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public class MovementAuthorityTransitionTests : MissionTestEnvironment
{
    private const string HostA = "host-a";
    private const string HostB = "host-b";
    private const string Observer = "observer-c";
    private const string BattleId = "movement-authority-battle";

    public MovementAuthorityTransitionTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DelayedFormerHostPacket_IsRejectedWhileNewHostAndFormerHostOwnPartyContinue(
        bool mount, bool compact)
    {
        using var engine = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, Observer);
        MovementFixture state = null;
        observer.Call(() => state = CreateFixture(engine, observer, mount, compact));

        observer.SimulatePacket(state.PeerA, state.Packet);
        observer.Call(() =>
        {
            Assert.Equal(state.Direction, state.Mirror.MovementDirection);
            state.Mirror.MovementDirection = Vec2.Zero;
            MigrateToHostB(observer, state.AgentId);
        });

        // Unreliable movement can arrive after the reliable host assignment and transfer.
        observer.SimulatePacket(state.PeerA, state.Packet);
        observer.Call(() => Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection));
        observer.SimulatePacket(state.PeerB, WithAuthority(state.Packet, HostB, 2));
        observer.Call(() => Assert.Equal(state.Direction, state.Mirror.MovementDirection));
        observer.SimulatePacket(state.PeerA, state.OwnPartyPacket);
        observer.Call(() =>
        {
            Assert.Equal(state.Direction, state.OwnPartyMirror.MovementDirection);
            Assert.True(observer.Resolve<INetworkAgentRegistry>()
                .TryGetAgentInfo(state.AgentId, out var info));
            Assert.Equal(HostB, info.CurrentAuthority);
            Assert.Equal(2, info.AuthorityRevision);
            Assert.Equal(HostA, info.MovementScopeId);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PacketQueuedBeforeAuthorityTransfer_IsRejectedAtGameThreadApply(bool mount)
    {
        using var engine = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, Observer);
        MovementFixture state = null;
        observer.Call(() => state = CreateFixture(engine, observer, mount, compact: true));

        observer.SimulatePacket(state.PeerA, state.Packet, markGameThread: false);
        Assert.Equal(1, observer.PendingGameThreadActionCount);
        observer.Call(() => MigrateToHostB(observer, state.AgentId));
        observer.PumpGameThread();
        observer.Call(() => Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection));

        observer.SimulatePacket(state.PeerB, WithAuthority(state.Packet, HostB, 2));
        observer.Call(() => Assert.Equal(state.Direction, state.Mirror.MovementDirection));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PacketQueuedBeforeSenderMappingChanges_IsRejectedAtGameThreadApply(
        bool mount, bool replaceMapping)
    {
        using var engine = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, Observer);
        MovementFixture state = null;
        observer.Call(() => state = CreateFixture(engine, observer, mount, compact: true));

        observer.SimulatePacket(state.PeerA, state.Packet, markGameThread: false);
        Assert.Equal(1, observer.PendingGameThreadActionCount);
        NetPeer replacement = NetPeerExtensions.CreatePeer(3);
        observer.Call(() =>
        {
            var context = observer.Resolve<IMissionContext>();
            if (replaceMapping)
                context.MapPeer(HostA, replacement);
            else
                context.RemovePeer(state.PeerA);
        });
        observer.PumpGameThread();
        observer.Call(() => Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection));

        observer.Call(() => observer.Resolve<IMissionContext>().MapPeer(HostA, replacement));
        observer.SimulatePacket(replacement, state.Packet);
        observer.Call(() => Assert.Equal(state.Direction, state.Mirror.MovementDirection));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CompressedRelay_RejectsOldGenerationWhenAuthorityReturnsToSameHost(bool mount, bool compact)
    {
        using var engine = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, Observer);
        observer.Call(() =>
        {
            var state = CreateFixture(engine, observer, mount, compact);
            state.Handler.Dispose();
            var relayPeer = NetPeerExtensions.CreatePeer(4);
            var relay = new Mock<IRelayNetwork>();
            relay.SetupGet(value => value.ServerEndpoint).Returns(relayPeer);
            using var handler = CreateHandler(observer, relay.Object);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var compressor = new MovementPacketCompressor(serializer);
            byte[] oldPayload = RelayPayload(state.Packet, serializer, compressor);

            observer.SimulatePacket(relayPeer, oldPayload);
            Assert.Equal(state.Direction, state.Mirror.MovementDirection);
            state.Mirror.MovementDirection = Vec2.Zero;
            MigrateToHostB(observer, state.AgentId);
            observer.SimulatePacket(relayPeer, oldPayload);
            Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection);
            observer.SimulatePacket(relayPeer,
                RelayPayload(WithAuthority(state.Packet, HostB, 2), serializer, compressor));
            Assert.Equal(state.Direction, state.Mirror.MovementDirection);

            state.Mirror.MovementDirection = Vec2.Zero;
            Assert.True(observer.Resolve<INetworkAgentRegistry>()
                .TryTransferAuthority(HostA, state.AgentId, 3));
            observer.Resolve<IBattleHostRegistry>().Set(
                BattleId, new BattleHostAssignment(HostA, new[] { HostB }, 3));
            observer.SimulatePacket(relayPeer, oldPayload);
            Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection);
            byte[] currentPayload = RelayPayload(
                WithAuthority(state.Packet, HostA, 3), serializer, compressor);
            observer.SimulatePacket(NetPeerExtensions.CreatePeer(5), currentPayload);
            Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection);
            observer.SimulatePacket(relayPeer, currentPayload);
            Assert.Equal(state.Direction, state.Mirror.MovementDirection);

            observer.SimulatePacket(relayPeer,
                RelayPayload(state.OwnPartyPacket, serializer, compressor));
            Assert.Equal(state.Direction, state.OwnPartyMirror.MovementDirection);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrMismatchedRevision_CannotApplyToTransferredAgent(bool mount)
    {
        using var engine = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, Observer);
        observer.Call(() =>
        {
            var state = CreateFixture(engine, observer, mount, compact: true);
            observer.SimulatePacket(state.PeerA, WithRevisions(state.Packet, null));
            observer.SimulatePacket(state.PeerA, WithRevisions(state.Packet, Array.Empty<long>()));
            observer.SimulatePacket(state.PeerA, WithRevisions(state.Packet, new long[] { -1 }));
            observer.SimulatePacket(state.PeerA, WithAuthority(state.Packet, HostB, 1));
            Assert.Equal(Vec2.Zero, state.Mirror.MovementDirection);
            observer.SimulatePacket(state.PeerA, state.Packet);
            Assert.Equal(state.Direction, state.Mirror.MovementDirection);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FragmentedPriorityBatches_PreserveCapturedRevisionsAcrossSerialization(bool mount, bool compact)
    {
        using var engine = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, Observer);
        observer.Call(() =>
        {
            var state = CreateFixture(engine, observer, mount, compact);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var compressor = new MovementPacketCompressor(serializer);
            var network = new Mock<IBattleNetwork>();
            var sent = new List<IPacket>();
            network.Setup(value => value.Send(It.IsAny<string>(), It.IsAny<IPacket>(), It.IsAny<byte[]>()))
                .Callback<string, IPacket, byte[]>((_, packet, bytes) =>
                {
                    Assert.True(bytes.Length <= 128);
                    Assert.True(compressor.TryRestore(serializer.Deserialize<IPacket>(bytes), out var restored));
                    sent.Add(restored);
                });
            var sender = new MovementBatchSender(network.Object, compressor,
                () => new MovementTrafficBudget(1000000, 1000000));
            var expected = new Dictionary<Guid, long>();
            var compactIds = new Dictionary<ushort, Guid>();
            if (mount)
            {
                var batch = CreateBatch(((MountMovementPacket)state.Packet).Mounts[0], compact, expected, compactIds);
                sender.Send(Observer, new[] { batch }, null, 128,
                    (scope, ids, guids, data) => scope == null
                        ? new MountMovementPacket(guids, data, HostB)
                        : new MountMovementPacket(scope, ids, data, HostB), (_, _) => { });
            }
            else
            {
                var batch = CreateBatch(((MovementPacket)state.Packet).Agents[0], compact, expected, compactIds);
                sender.Send(Observer, new[] { batch }, null, 128,
                    (scope, ids, guids, data) => scope == null
                        ? new MovementPacket(guids, data, HostB)
                        : new MovementPacket(scope, ids, data, HostB), (_, _) => { });
            }

            Assert.True(sent.Count > 1);
            var seen = new List<Guid>();
            foreach (IPacket packet in sent)
            {
                ushort[] ids;
                Guid[] guids;
                long[] revisions;
                if (packet is MovementPacket movement)
                {
                    Assert.Equal(HostB, movement.SenderControllerId);
                    ids = movement.AgentIds;
                    guids = movement.AgentGuids;
                    revisions = movement.AuthorityRevisions;
                }
                else
                {
                    var mounts = (MountMovementPacket)packet;
                    Assert.Equal(HostB, mounts.SenderControllerId);
                    ids = mounts.MountIds;
                    guids = mounts.MountGuids;
                    revisions = mounts.AuthorityRevisions;
                }
                Guid[] actualIds = guids ?? ids.Select(id => compactIds[id]).ToArray();
                Assert.Equal(actualIds.Length, revisions.Length);
                for (int i = 0; i < actualIds.Length; i++)
                    Assert.Equal(expected[actualIds[i]], revisions[i]);
                seen.AddRange(actualIds);
            }
            Assert.Equal(expected.Count, seen.Distinct().Count());
            Assert.Equal(expected.Count, seen.Count);
        });
    }

    private static MovementBatch<T> CreateBatch<T>(T data, bool compact,
        Dictionary<Guid, long> expected, Dictionary<ushort, Guid> compactIds)
    {
        var batch = new MovementBatch<T>(compact ? HostA : null);
        for (ushort i = 1; i <= 24; i++)
        {
            Guid id = Guid.NewGuid();
            long revision = 123456789L * i;
            var info = new CoopAgentInfo(HostB, HostA, HostA, null, id, i, revision);
            batch.Add(info, data, new MovementPriorityKey(1, 24 - i, 0, 0, id));
            expected[id] = revision;
            compactIds[i] = id;
            info.AuthorityRevision++;
        }
        return batch;
    }

    private static AgentMovementHandler CreateHandler(EnvironmentInstance observer, IRelayNetwork relay) => new(
        observer.Resolve<IBattleNetwork>(), observer.Resolve<IPacketManager>(),
        observer.Resolve<IMessageBroker>(), observer.Resolve<INetworkAgentRegistry>(),
        observer.Resolve<IControllerIdProvider>(), observer.Resolve<IAgentEquipmentApplier>(),
        observer.Resolve<IMovementBatchSender>(), observer.Resolve<IPuppetMountStateRepairer>(),
        observer.Resolve<IAgentVisualActionAccessor>(), observer.Resolve<IMovementRateController>(),
        observer.Resolve<IMovementPriorityScheduler>(), observer.Resolve<IMissionContext>(), relay);

    private static byte[] RelayPayload(IPacket packet, ProtoBufSerializer serializer, MovementPacketCompressor compressor)
    {
        // Repeated entries keep this test on the compressed path for either snapshot shape.
        if (packet is MovementPacket movement)
        {
            packet = movement.AgentIds == null
                ? new MovementPacket(Enumerable.Repeat(movement.AgentGuids[0], 8).ToArray(),
                    Enumerable.Repeat(movement.Agents[0], 8).ToArray(), movement.SenderControllerId,
                    Enumerable.Repeat(movement.AuthorityRevisions[0], 8).ToArray())
                : new MovementPacket(movement.IdentityScopeId, Enumerable.Repeat(movement.AgentIds[0], 8).ToArray(),
                    Enumerable.Repeat(movement.Agents[0], 8).ToArray(), movement.SenderControllerId,
                    Enumerable.Repeat(movement.AuthorityRevisions[0], 8).ToArray());
        }
        else
        {
            var mounts = (MountMovementPacket)packet;
            packet = mounts.MountIds == null
                ? new MountMovementPacket(Enumerable.Repeat(mounts.MountGuids[0], 8).ToArray(),
                    Enumerable.Repeat(mounts.Mounts[0], 8).ToArray(), mounts.SenderControllerId,
                    Enumerable.Repeat(mounts.AuthorityRevisions[0], 8).ToArray())
                : new MountMovementPacket(mounts.IdentityScopeId, Enumerable.Repeat(mounts.MountIds[0], 8).ToArray(),
                    Enumerable.Repeat(mounts.Mounts[0], 8).ToArray(), mounts.SenderControllerId,
                    Enumerable.Repeat(mounts.AuthorityRevisions[0], 8).ToArray());
        }
        byte[] bytes = compressor.Serialize(packet);
        Assert.IsType<CompressedMovementPacket>(serializer.Deserialize<IPacket>(bytes));
        var envelope = new RelayPacket(packet.DeliveryMethod, BattleId, Observer, bytes);
        return serializer.Deserialize<RelayPacket>(serializer.Serialize(envelope)).Payload;
    }

    private static IPacket WithRevisions(IPacket packet, long[] revisions) => packet is MovementPacket movement
        ? movement.WithAuthorityRevisions(revisions)
        : ((MountMovementPacket)packet).WithAuthorityRevisions(revisions);

    private MovementFixture CreateFixture(
        MissionEngineFixture engine, EnvironmentInstance observer, bool mount, bool compact)
    {
        MockMission mission = engine.CreateMission(observer);
        var state = new MovementFixture();
        var broker = observer.Resolve<IMessageBroker>();
        broker.Publish(this, new NetworkMissionPeerEntered(HostA, BattleId));
        broker.Publish(this, new NetworkMissionPeerEntered(HostB, BattleId));
        observer.Resolve<IMissionContext>().MapPeer(HostA, state.PeerA);
        observer.Resolve<IMissionContext>().MapPeer(HostB, state.PeerB);
        observer.Resolve<IBattleHostRegistry>().Set(
            BattleId, new BattleHostAssignment(HostA, new[] { HostB }, 1));
        state.Handler = observer.Resolve<ICoopMissionComponent>().AgentMovementHandler;

        Agent puppet = Spawn(mission, mount);
        Agent ownPartyPuppet = Spawn(mission, mount);
        Agent source = Spawn(mission, mount);
        Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));
        Assert.True(AgentMirror.TryGet(ownPartyPuppet, out var ownPartyMirror));
        Assert.True(AgentMirror.TryGet(source, out var sourceMirror));
        state.Mirror = puppetMirror;
        state.OwnPartyMirror = ownPartyMirror;
        state.Mirror.MovementDirection = Vec2.Zero;
        state.OwnPartyMirror.MovementDirection = Vec2.Zero;
        sourceMirror.MovementDirection = state.Direction;
        var registry = observer.Resolve<INetworkAgentRegistry>();
        Assert.True(registry.TryRegisterAgent(HostA, state.AgentId, 1, puppet, 1));
        Assert.True(registry.TryRegisterAgent(HostA, state.OwnPartyId, 2, ownPartyPuppet, 0));
        state.Packet = WithAuthority(CreatePacket(source, state.AgentId, 1, mount, compact), HostA, 1);
        state.OwnPartyPacket = WithAuthority(CreatePacket(source, state.OwnPartyId, 2, mount, compact), HostA, 0);
        return state;
    }

    private static void MigrateToHostB(EnvironmentInstance observer, Guid agentId)
    {
        var hosts = observer.Resolve<IBattleHostRegistry>();
        Assert.True(hosts.TryGet(BattleId, out var previous));
        Assert.Equal(1, previous.Epoch);
        hosts.Set(BattleId, new BattleHostAssignment(HostB, Array.Empty<string>(), 2));
        Assert.True(observer.Resolve<INetworkAgentRegistry>()
            .TryTransferAuthority(HostB, agentId, 2));
        Assert.True(hosts.TryGet(BattleId, out var current));
        Assert.Equal(2, current.Epoch);
    }

    private static Agent Spawn(MockMission mission, bool mount) => mount
        ? mission.SpawnMount()
        : mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
            .Controller(AgentControllerType.None));

    private static IPacket CreatePacket(
        Agent source, Guid id, ushort compactId, bool mount, bool compact)
    {
        if (mount)
        {
            var data = new[] { new AgentMountData(source, id) };
            return compact
                ? new MountMovementPacket(HostA, new[] { compactId }, data)
                : new MountMovementPacket(new[] { id }, data);
        }

        var riders = new[] { new AgentData(source) };
        return compact
            ? new MovementPacket(HostA, new[] { compactId }, riders)
            : new MovementPacket(new[] { id }, riders);
    }

    private static IPacket WithAuthority(IPacket packet, string sender, long revision)
    {
        if (packet is MovementPacket movement)
        {
            var revisions = Enumerable.Repeat(revision, movement.Agents.Length).ToArray();
            return movement.AgentIds == null
                ? new MovementPacket(movement.AgentGuids, movement.Agents, sender, revisions)
                : new MovementPacket(movement.IdentityScopeId, movement.AgentIds, movement.Agents, sender, revisions);
        }

        var mounts = (MountMovementPacket)packet;
        var mountRevisions = Enumerable.Repeat(revision, mounts.Mounts.Length).ToArray();
        return mounts.MountIds == null
            ? new MountMovementPacket(mounts.MountGuids, mounts.Mounts, sender, mountRevisions)
            : new MountMovementPacket(mounts.IdentityScopeId, mounts.MountIds, mounts.Mounts, sender, mountRevisions);
    }

    private sealed class MovementFixture
    {
        public readonly NetPeer PeerA = NetPeerExtensions.CreatePeer(1);
        public readonly NetPeer PeerB = NetPeerExtensions.CreatePeer(2);
        public readonly Guid AgentId = Guid.NewGuid();
        public readonly Guid OwnPartyId = Guid.NewGuid();
        public readonly Vec2 Direction = new Vec2(1f, 0f);
        public MirrorAgent Mirror;
        public MirrorAgent OwnPartyMirror;
        public IAgentMovementHandler Handler;
        public IPacket Packet;
        public IPacket OwnPartyPacket;
    }
}
