using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Services.Network;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

// #4487: snapshots encoded once per capture and shared across recipients must send the same datagrams as before.
public partial class MovementTrafficTests
{
    private static readonly string[] EncodeOnceRecipients =
    {
        "76561198000000101",
        "76561198000000102",
        "76561198000000103",
    };

    [Theory]
    [InlineData(1024 * 1024, 1024 * 1024, 3)]
    [InlineData(40_000, 2_500, 6)]
    public void EncodeOnce_RecipientsAndCapturesSendLegacyDatagrams(int bytesPerSecond, int burstBytes, int frames)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        EncodeOnceRun legacyRun = null!;
        EncodeOnceRun currentRun = null!;

        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            List<MovementSnapshots> captures = CreateEncodeOnceCaptures(mock, riders: 72, mounts: 24, frames);
            Func<IMovementTrafficBudget> budgets = () => new MovementTrafficBudget(bytesPerSecond, burstBytes);

            legacyRun = RunEncodeOnce(serializer, new LegacyMovementPacketCompressor(serializer), captures, budgets);
            currentRun = RunEncodeOnce(serializer, new MovementPacketCompressor(serializer), captures, budgets);
        });

        Assert.Equal(legacyRun.Frames, currentRun.Frames);
        Assert.Equal(legacyRun.SentAgents, currentRun.SentAgents);
        Assert.Equal(legacyRun.Datagrams.Count, currentRun.Datagrams.Count);
        for (int i = 0; i < legacyRun.Datagrams.Count; i++)
        {
            Assert.Equal(legacyRun.Datagrams[i].Recipient, currentRun.Datagrams[i].Recipient);
            Assert.Equal(legacyRun.Datagrams[i].Payload, currentRun.Datagrams[i].Payload);
        }

        // The run has to reach every path the cache changes: several recipients and captures, envelopes and decks.
        Assert.All(EncodeOnceRecipients, recipient =>
            Assert.Contains(currentRun.Datagrams, datagram => datagram.Recipient == recipient));
        Assert.True(currentRun.SequencesSent.Count > 1, "Expected several captures to send");
        Assert.Contains(currentRun.Datagrams, datagram => datagram.Envelope);
        Assert.Contains(currentRun.Datagrams, datagram => !datagram.Envelope);
        Assert.True(currentRun.DeckPackets > 0, "Expected packets carrying deck poses");
        output.WriteLine(string.Join(System.Environment.NewLine, currentRun.Frames));
    }

    [Fact]
    public void EncodeOnce_UncompressedPayloadFillingAPooledArrayStaysOwned()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var legacy = new LegacyMovementPacketCompressor(serializer);
        var compressor = new MovementPacketCompressor(serializer);
        // 8,192 bytes is one whole pooled array, and too large to compress, so the spliced bytes are the payload.
        string scope = CreateIncompressibleString(8000);
        while (legacy.Serialize(CreateOversizedPacket(scope, 11)).Length < 8192)
            scope += "x";
        MovementPacket first = CreateOversizedPacket(scope, 11);
        Assert.Equal(8192, legacy.Serialize(first).Length);

        byte[] firstPayload = compressor.SerializeSnapshots(first, new[] { EquivalenceGuid(1) });
        string reversed = new string(scope.Reverse().ToArray());
        compressor.SerializeSnapshots(CreateOversizedPacket(reversed, 12), new[] { EquivalenceGuid(1) });

        Assert.Equal(legacy.Serialize(first), firstPayload);
    }

    private static MovementPacket CreateOversizedPacket(string scope, long sequence) =>
        new MovementPacket(scope, new ushort[] { 1 }, new[] { default(AgentData) }, EquivalenceSender, new long[] { 1 }, sequence);

    private static EncodeOnceRun RunEncodeOnce(
        ICommonSerializer serializer,
        IMovementPacketCompressor compressor,
        List<MovementSnapshots> captures,
        Func<IMovementTrafficBudget> budgets)
    {
        var run = new EncodeOnceRun();
        var network = new DatagramRecordingNetwork(serializer, run);
        var sender = new MovementBatchSender(network, compressor, budgets);
        int payloadBudget = LiteNetP2PClient.CalculateMaxRelayPayloadBytes(
            serializer,
            EquivalenceScope,
            EncodeOnceRecipients[0],
            LiteNetP2PClient.SafeSinglePacketBytes);

        for (int frame = 0; frame < captures.Count; frame++)
        {
            MovementSnapshots capture = captures[frame];
            long sequence = 11 + frame;
            IPacket CreateRiders(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentData[] data)
            {
                Guid[]? deckShips = IndexEncodeOnceDecks(data);
                if (deckShips != null) run.DeckPackets++;
                return scope == null
                    ? new MovementPacket(canonicalIds, data, EquivalenceSender, sampleSequence: sequence, deckShips: deckShips!)
                    : new MovementPacket(scope, compactIds, data, EquivalenceSender, sampleSequence: sequence, deckShips: deckShips!);
            }
            IPacket CreateMounts(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentMountData[] data) =>
                scope == null
                    ? new MountMovementPacket(canonicalIds, data, EquivalenceSender, sampleSequence: sequence)
                    : new MountMovementPacket(scope, compactIds, data, EquivalenceSender, sampleSequence: sequence);

            sender.BeginFrame(frame == 0 ? 0f : 0.025f);
            foreach (string recipient in EncodeOnceRecipients)
            {
                MovementSendPairResult result = sender.SendInterleaved(
                    recipient,
                    capture.RiderBatches,
                    capture.LegacyRiders,
                    CreateRiders,
                    (agentId, _) => run.SentAgents.Add($"{recipient} {agentId}"),
                    capture.MountBatches,
                    capture.LegacyMounts,
                    CreateMounts,
                    (agentId, _) => run.SentAgents.Add($"{recipient} {agentId}"),
                    payloadBudget);
                MovementTrafficFrame traffic = sender.EndFrame(
                    recipient,
                    result.First.DeferredCount + result.Second.DeferredCount,
                    0f);
                if (result.First.SentCount + result.Second.SentCount > 0)
                    run.SequencesSent.Add(sequence);
                run.Frames.Add($"frame {frame} {recipient}: riders {Describe(result.First)}; mounts {Describe(result.Second)}; {traffic.SentBytes} B");
            }
        }

        return run;
    }

    // Same per-packet hull table as AgentMovementHandler.IndexDecks.
    private static Guid[]? IndexEncodeOnceDecks(AgentData[] data)
    {
        List<Guid>? ships = null;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].DeckShip == Guid.Empty) continue;

            ships ??= new List<Guid>();
            int index = ships.IndexOf(data[i].DeckShip);
            if (index < 0)
            {
                ships.Add(data[i].DeckShip);
                index = ships.Count - 1;
            }
            data[i].IndexDeck(index + 1);
        }
        return ships?.ToArray();
    }

    // One capture per frame over the same agents and ids, with every agent's state changed between captures.
    private static List<MovementSnapshots> CreateEncodeOnceCaptures(MockMission mock, int riders, int mounts, int frames)
    {
        var hulls = new[] { EquivalenceGuid(9001), EquivalenceGuid(9002) };
        var riderAgents = new List<(Agent Rider, Agent Mount)>();
        for (int i = 0; i < riders; i++)
        {
            Agent rider = SpawnRider(mock);
            riderAgents.Add((rider, i % 3 == 0 ? mock.SpawnMount(rider) : null));
        }
        var mountAgents = new List<Agent>();
        for (int i = 0; i < mounts; i++)
            mountAgents.Add(mock.SpawnMount());

        var captures = new List<MovementSnapshots>();
        for (int frame = 0; frame < frames; frame++)
        {
            int seed = frame * 7919;
            var snapshots = new MovementSnapshots(EquivalenceScope);
            for (int i = 0; i < riders; i++)
            {
                (Agent rider, Agent mount) = riderAgents[i];
                Assert.True(AgentMirror.TryGet(rider, out var mirror));
                PopulateEquivalenceState(mirror, seed + i + 1, compressible: i % 4 == 0);
                AgentData data;
                if (mount != null)
                {
                    Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
                    PopulateEquivalenceState(mountMirror, seed + i + 501, compressible: false);
                    data = new AgentData(rider, (ushort)(2000 + i), EquivalenceScope, EquivalenceGuid(2000 + i), mountAuthorityRevision: i);
                }
                else
                {
                    data = new AgentData(rider);
                }
                if (i % 9 == 4)
                    data.CaptureDeck(hulls[i % 2], new Vec3(i * 0.25f, -i * 0.5f, 0.75f + frame), 1.5f);

                MovementBatch<AgentData> batch = i < 2
                    ? snapshots.RiderBatches[0]
                    : i % 7 == 0 ? snapshots.LegacyRiders : snapshots.RiderBatches[1];
                var info = new CoopAgentInfo("peer", "peer", batch.IdentityScopeId, rider, EquivalenceGuid(i), batch.IdentityScopeId == null ? (ushort)0 : (ushort)(i + 1), i + 1);
                double score = batch == snapshots.LegacyRiders ? 40 + (i % 5) : (i + frame) % 37;
                batch.Add(info, data, new MovementPriorityKey(i < 2 ? 0 : 1, score, 0f, 0f, info.AgentId));
            }

            for (int i = 0; i < mounts; i++)
            {
                Agent mount = mountAgents[i];
                Assert.True(AgentMirror.TryGet(mount, out var mirror));
                PopulateEquivalenceState(mirror, seed + i + 1001, compressible: i % 2 == 0);
                MovementBatch<AgentMountData> batch = i % 5 == 0 ? snapshots.LegacyMounts : snapshots.MountBatches[0];
                var info = new CoopAgentInfo("peer", "peer", batch.IdentityScopeId, mount, EquivalenceGuid(1000 + i), batch.IdentityScopeId == null ? (ushort)0 : (ushort)(1000 + i), i + 1);
                double score = batch == snapshots.LegacyMounts ? 45 + (i % 3) : 28 + ((i + frame) % 11);
                batch.Add(info, new AgentMountData(mount), new MovementPriorityKey(1, score, 0f, 0f, info.AgentId));
            }

            captures.Add(snapshots);
        }
        return captures;
    }

    private sealed class EncodeOnceRun
    {
        public List<string> Frames { get; } = new();
        public List<string> SentAgents { get; } = new();
        public List<(string Recipient, byte[] Payload, bool Envelope)> Datagrams { get; } = new();
        public HashSet<long> SequencesSent { get; } = new();
        public int DeckPackets { get; set; }
    }

    private sealed class DatagramRecordingNetwork : IBattleNetwork
    {
        private readonly ICommonSerializer serializer;
        private readonly EncodeOnceRun run;

        public DatagramRecordingNetwork(ICommonSerializer serializer, EncodeOnceRun run)
        {
            this.serializer = serializer;
            this.run = run;
        }

        public void Send(string controllerId, IPacket packet, byte[] serializedPacket) =>
            run.Datagrams.Add((controllerId, serializedPacket, serializer.Deserialize<IPacket>(serializedPacket) is CompressedMovementPacket));

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
