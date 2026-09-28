using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Serialization;
using System.Text.RegularExpressions;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Services.Network;
using TaleWorlds.MountAndBlade;
using Xunit;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

// Movement sends through the frozen pre-#3076 serializer and the current one, packet for packet.
public partial class MovementTrafficTests
{
    private const string EquivalenceScope = "MapEvent_Created_0000";
    private const string EquivalenceRecipient = "76561198000000042";
    private const string EquivalenceSender = "76561198000000077";

    [Fact]
    public void SerializerEquivalence_RealMovementPacketsKeepLegacyBytes()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            var mapper = new SerializableTypeMapper();
            var legacy = new LegacyProtoBufSerializer(mapper);
            var current = new ProtoBufSerializer(mapper);
            MovementSnapshots incompressible = CreateEquivalenceSnapshots(mock, riders: 8, mounts: 8, compressible: false, EquivalenceScope);
            MovementSnapshots compressible = CreateEquivalenceSnapshots(mock, riders: 8, mounts: 0, compressible: true, EquivalenceScope);
            MovementBatch<AgentData> riders = incompressible.RiderBatches[1];
            MovementBatch<AgentMountData> mounts = incompressible.MountBatches[0];
            long[] revisions = riders.AuthorityRevisions.ToArray();
            var packets = new IPacket[]
            {
                new MovementPacket(EquivalenceScope, riders.CompactIds.ToArray(), riders.Data.ToArray(), EquivalenceSender, revisions, 11),
                new MovementPacket(riders.CanonicalIds.ToArray(), riders.Data.ToArray(), EquivalenceSender, revisions, 11),
                new MountMovementPacket(EquivalenceScope, mounts.CompactIds.ToArray(), mounts.Data.ToArray(), EquivalenceSender, mounts.AuthorityRevisions.ToArray(), 11),
                new MountMovementPacket(mounts.CanonicalIds.ToArray(), mounts.Data.ToArray(), EquivalenceSender, mounts.AuthorityRevisions.ToArray(), 11),
                new MovementPacket(EquivalenceScope, compressible.RiderBatches[1].CompactIds.ToArray(), compressible.RiderBatches[1].Data.ToArray(), EquivalenceSender, compressible.RiderBatches[1].AuthorityRevisions.ToArray(), 11),
                new MovementPacket(Array.Empty<Guid>(), Array.Empty<AgentData>()),
                new CompressedMovementPacket(512, new byte[] { 1, 2, 3 }),
            };
            var legacyCompressor = new MovementPacketCompressor(legacy);
            var currentCompressor = new MovementPacketCompressor(current);
            int envelopes = 0;

            foreach (IPacket packet in packets)
            {
                byte[] expected = legacy.Serialize(packet);
                Assert.Equal(expected, current.Serialize(packet));
                Assert.Equal(expected, legacy.Serialize(current.Deserialize<IPacket>(expected)));
                Assert.Equal(expected, current.Serialize(legacy.Deserialize<IPacket>(expected)));

                byte[] expectedWire = legacyCompressor.Serialize(packet);
                Assert.Equal(expectedWire, currentCompressor.Serialize(packet));
                if (legacy.Deserialize<IPacket>(expectedWire) is CompressedMovementPacket) envelopes++;
            }

            Assert.True(envelopes > 0, "Expected at least one LZ4 envelope");
        });
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
                LiteNetP2PClient.CalculateMaxRelayPayloadBytes(legacy, EquivalenceScope, EquivalenceRecipient, ceiling),
                LiteNetP2PClient.CalculateMaxRelayPayloadBytes(current, EquivalenceScope, EquivalenceRecipient, ceiling));
        }
    }

    [Fact]
    public void SerializerEquivalence_InterleavedRidersAndMountsSendIdenticalPackets()
    {
        MovementSenderRun run = RunSenderEquivalence(
            riders: 125,
            mounts: 48,
            compressible: false,
            EquivalenceScope,
            () => new MovementTrafficBudget(),
            frames: 2);

        Assert.True(run.Sends.Count > 4, $"Expected several packets, got {run.Sends.Count}");
        Assert.True(run.Candidates.Count > run.Sends.Count, "Expected the size search to probe more candidates than it sends");
        Assert.Contains(run.Sends, send => send.StartsWith("Movement ", StringComparison.Ordinal));
        Assert.Contains(run.Sends, send => send.StartsWith("MountMovement ", StringComparison.Ordinal));
        Assert.Contains(run.Sends, send => send.Contains(" guid ", StringComparison.Ordinal));
    }

    [Fact]
    public void SerializerEquivalence_CompressibleSnapshotsChooseTheSameEnvelopes()
    {
        MovementSenderRun run = RunSenderEquivalence(
            riders: 90,
            mounts: 30,
            compressible: true,
            EquivalenceScope,
            () => new MovementTrafficBudget(),
            frames: 1);

        Assert.Contains(run.Sends, send => send.EndsWith(" lz4", StringComparison.Ordinal));
        Assert.Contains(run.Candidates, candidate => candidate.EndsWith(" raw", StringComparison.Ordinal));
    }

    [Fact]
    public void SerializerEquivalence_OversizedScopeFallsBackToGuidsIdentically()
    {
        MovementSenderRun run = RunSenderEquivalence(
            riders: 13,
            mounts: 0,
            compressible: false,
            CreateIncompressibleString(3000),
            () => new MovementTrafficBudget(),
            frames: 2);

        Assert.Contains(run.Candidates, candidate => candidate.Contains(" compact ", StringComparison.Ordinal));
        Assert.All(run.Sends, send => Assert.Contains(" guid ", send, StringComparison.Ordinal));
    }

    [Fact]
    public void SerializerEquivalence_TightBudgetDefersTheSameSnapshotsAcrossFrames()
    {
        MovementSenderRun run = RunSenderEquivalence(
            riders: 60,
            mounts: 20,
            compressible: false,
            EquivalenceScope,
            () => new MovementTrafficBudget(40_000, 2_500),
            frames: 8);

        Assert.Contains(run.Frames, frame => Regex.IsMatch(frame, "deferred [1-9]"));
        Assert.True(run.Frames.Count(frame => Regex.IsMatch(frame, "^frame [1-9].*riders sent [1-9]")) > 1,
            "Expected later frames to keep sending as the budget refills");
    }

    private MovementSenderRun RunSenderEquivalence(
        int riders,
        int mounts,
        bool compressible,
        string scope,
        Func<IMovementTrafficBudget> budgetFactory,
        int frames)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        MovementSenderRun currentRun = null!;

        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            MovementSnapshots snapshots = CreateEquivalenceSnapshots(mock, riders, mounts, compressible, scope);
            var mapper = new SerializableTypeMapper();

            MovementSenderRun legacyRun = RunSender(new LegacyProtoBufSerializer(mapper), snapshots, budgetFactory, frames);
            currentRun = RunSender(new ProtoBufSerializer(mapper), snapshots, budgetFactory, frames);

            Assert.Equal(legacyRun.Frames, currentRun.Frames);
            Assert.Equal(legacyRun.Candidates, currentRun.Candidates);
            Assert.Equal(legacyRun.Sends, currentRun.Sends);
            Assert.Equal(legacyRun.SentAgents, currentRun.SentAgents);
            Assert.Equal(legacyRun.Payloads.Count, currentRun.Payloads.Count);
            for (int i = 0; i < legacyRun.Payloads.Count; i++)
                Assert.Equal(legacyRun.Payloads[i], currentRun.Payloads[i]);
        });

        output.WriteLine(string.Join(System.Environment.NewLine, currentRun.Frames));
        return currentRun;
    }

    private static MovementSenderRun RunSender(
        ICommonSerializer serializer,
        MovementSnapshots snapshots,
        Func<IMovementTrafficBudget> budgetFactory,
        int frames)
    {
        var run = new MovementSenderRun();
        var network = new RecordingBattleNetwork(serializer, run);
        var sender = new MovementBatchSender(
            network,
            new RecordingMovementCompressor(new MovementPacketCompressor(serializer), serializer, run),
            budgetFactory);
        int payloadBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
            serializer,
            EquivalenceScope,
            EquivalenceRecipient,
            LiteNetP2PClient.SafeSinglePacketBytes);
        run.Frames.Add($"payload budget {payloadBudget}");

        for (int frame = 0; frame < frames; frame++)
        {
            sender.BeginFrame(frame == 0 ? 0f : 0.025f);
            MovementSendPairResult result = sender.SendInterleaved(
                EquivalenceRecipient,
                snapshots.RiderBatches,
                snapshots.LegacyRiders,
                CreateEquivalenceMovementPacket,
                (agentId, _) => run.SentAgents.Add(agentId),
                snapshots.MountBatches,
                snapshots.LegacyMounts,
                CreateEquivalenceMountPacket,
                (agentId, _) => run.SentAgents.Add(agentId),
                payloadBudget);
            MovementTrafficFrame traffic = sender.EndFrame(
                EquivalenceRecipient,
                result.First.DeferredCount + result.Second.DeferredCount,
                0f);
            run.Frames.Add($"frame {frame}: riders {Describe(result.First)}; mounts {Describe(result.Second)}; {traffic.SentBytes} B");
        }

        return run;
    }

    private static string Describe(MovementSendResult result) =>
        $"sent {result.SentCount} priority {result.PrioritySentCount} deferred {result.DeferredCount} " +
        $"blocked {result.BlockedBySharedBudget}/{result.PriorityBlockedBySharedBudget} needs {result.RequiredSharedBudgetBytes}";

    private static IPacket CreateEquivalenceMovementPacket(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentData[] data) =>
        scope == null
            ? new MovementPacket(canonicalIds, data, EquivalenceSender, sampleSequence: 11)
            : new MovementPacket(scope, compactIds, data, EquivalenceSender, sampleSequence: 11);

    private static IPacket CreateEquivalenceMountPacket(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentMountData[] data) =>
        scope == null
            ? new MountMovementPacket(canonicalIds, data, EquivalenceSender, sampleSequence: 11)
            : new MountMovementPacket(scope, compactIds, data, EquivalenceSender, sampleSequence: 11);

    // A priority batch, a scoped batch and a guid-only legacy batch per kind. Early scoped riders outrank every
    // other cursor, so their packets grow to the size limit; late riders and scoped mounts interleave.
    private static MovementSnapshots CreateEquivalenceSnapshots(
        MockMission mock,
        int riders,
        int mounts,
        bool compressible,
        string scope)
    {
        var snapshots = new MovementSnapshots(scope);
        for (int i = 0; i < riders; i++)
        {
            Agent rider = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(rider, out var mirror));
            PopulateEquivalenceState(mirror, i + 1, compressible);
            AgentData data;
            if (i % 3 == 0)
            {
                Agent mount = mock.SpawnMount(rider);
                Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
                PopulateEquivalenceState(mountMirror, i + 501, compressible);
                data = new AgentData(rider, (ushort)(2000 + i), scope, EquivalenceGuid(2000 + i), mountAuthorityRevision: i);
            }
            else
            {
                data = new AgentData(rider);
            }

            MovementBatch<AgentData> batch = i < 2
                ? snapshots.RiderBatches[0]
                : i % 7 == 0 ? snapshots.LegacyRiders : snapshots.RiderBatches[1];
            var info = new CoopAgentInfo("peer", "peer", batch.IdentityScopeId, rider, EquivalenceGuid(i), batch.IdentityScopeId == null ? (ushort)0 : (ushort)(i + 1), i + 1);
            double score = batch == snapshots.LegacyRiders ? 40 + (i % 5) : i / 4;
            batch.Add(info, data, new MovementPriorityKey(i < 2 ? 0 : 1, score, 0f, 0f, info.AgentId));
        }

        for (int i = 0; i < mounts; i++)
        {
            Agent mount = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(mount, out var mirror));
            PopulateEquivalenceState(mirror, i + 1001, compressible);
            MovementBatch<AgentMountData> batch = i % 5 == 0 ? snapshots.LegacyMounts : snapshots.MountBatches[0];
            var info = new CoopAgentInfo("peer", "peer", batch.IdentityScopeId, mount, EquivalenceGuid(1000 + i), batch.IdentityScopeId == null ? (ushort)0 : (ushort)(1000 + i), i + 1);
            double score = batch == snapshots.LegacyMounts ? 45 + (i % 3) : 28 + (i % 11);
            batch.Add(info, new AgentMountData(mount), new MovementPriorityKey(1, score, 0f, 0f, info.AgentId));
        }

        return snapshots;
    }

    private static void PopulateEquivalenceState(MirrorAgent mirror, int seed, bool compressible)
    {
        if (compressible) PopulateMovementState(mirror, 1);
        else PopulateIncompressibleMovementState(mirror, seed);
    }

    private static Guid EquivalenceGuid(int seed)
    {
        var bytes = new byte[16];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((seed * 31) + (i * 17) + 1);
        return new Guid(bytes);
    }

    private static string DescribeIds(IPacket packet) => packet switch
    {
        MovementPacket movement => movement.AgentIds != null
            ? $"compact x{movement.AgentIds.Length}"
            : $"guid x{movement.AgentGuids?.Length ?? 0}",
        MountMovementPacket mount => mount.MountIds != null
            ? $"compact x{mount.MountIds.Length}"
            : $"guid x{mount.MountGuids?.Length ?? 0}",
        _ => "other",
    };

    private sealed class MovementSnapshots
    {
        public MovementSnapshots(string scope)
        {
            RiderBatches = new List<MovementBatch<AgentData>>
            {
                // Own scope, so its short batch does not pin the bulk batch's preferred size.
                new MovementBatch<AgentData>(scope + "-priority", isPriority: true),
                new MovementBatch<AgentData>(scope),
            };
            MountBatches = new List<MovementBatch<AgentMountData>> { new MovementBatch<AgentMountData>(scope) };
        }

        public List<MovementBatch<AgentData>> RiderBatches { get; }
        public MovementBatch<AgentData> LegacyRiders { get; } = new MovementBatch<AgentData>(null);
        public List<MovementBatch<AgentMountData>> MountBatches { get; }
        public MovementBatch<AgentMountData> LegacyMounts { get; } = new MovementBatch<AgentMountData>(null);
    }

    private sealed class MovementSenderRun
    {
        public List<string> Frames { get; } = new();
        public List<string> Candidates { get; } = new();
        public List<string> Sends { get; } = new();
        public List<byte[]> Payloads { get; } = new();
        public List<Guid> SentAgents { get; } = new();
    }

    private sealed class RecordingMovementCompressor : IMovementPacketCompressor
    {
        private readonly IMovementPacketCompressor inner;
        private readonly ICommonSerializer serializer;
        private readonly MovementSenderRun run;

        public RecordingMovementCompressor(IMovementPacketCompressor inner, ICommonSerializer serializer, MovementSenderRun run)
        {
            this.inner = inner;
            this.serializer = serializer;
            this.run = run;
        }

        public byte[] Serialize(IPacket packet)
        {
            byte[] payload = inner.Serialize(packet);
            run.Candidates.Add(DescribeWire(serializer, packet, payload));
            return payload;
        }

        public bool TryRestore(IPacket packet, out IPacket restored) => inner.TryRestore(packet, out restored);
    }

    private static string DescribeWire(ICommonSerializer serializer, IPacket packet, byte[] payload)
    {
        bool envelope = serializer.Deserialize<IPacket>(payload) is CompressedMovementPacket;
        return $"{packet.PacketType} {DescribeIds(packet)} {payload.Length} B {(envelope ? "lz4" : "raw")}";
    }

    private sealed class RecordingBattleNetwork : IBattleNetwork
    {
        private readonly ICommonSerializer serializer;
        private readonly MovementSenderRun run;

        public RecordingBattleNetwork(ICommonSerializer serializer, MovementSenderRun run)
        {
            this.serializer = serializer;
            this.run = run;
        }

        public void Send(string controllerId, IPacket packet, byte[] serializedPacket)
        {
            Assert.Equal(EquivalenceRecipient, controllerId);
            run.Sends.Add(DescribeWire(serializer, packet, serializedPacket));
            run.Payloads.Add(serializedPacket);
        }

        public void ConnectToInstance(string instanceId) => throw new NotSupportedException();
        public void Start() => throw new NotSupportedException();
        public void Stop() => throw new NotSupportedException();
        public void Send(string controllerId, IPacket packet) => throw new NotSupportedException();
        public void SendAll(IPacket packet) => throw new NotSupportedException();
        public void SendAll(IPacket packet, byte[] serializedPacket) => throw new NotSupportedException();
        public void SendAllBut(string controllerId, IPacket packet) => throw new NotSupportedException();
        public void Send(string controllerId, IMessage message) => throw new NotSupportedException();
        public void SendAll(IMessage message) => throw new NotSupportedException();
        public void SendAllBut(string controllerId, IMessage message) => throw new NotSupportedException();
        public int GetMaxUnreliablePayloadBytes(string controllerId) => throw new NotSupportedException();
        public int GetMaxUnreliablePayloadBytes() => throw new NotSupportedException();
    }
}
