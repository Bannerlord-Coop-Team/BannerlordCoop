using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.MockEngine;
using K4os.Compression.LZ4;
using LiteNetLib;
using Missions.Agents.Packets;
using Missions.Services.Network;
using TaleWorlds.Library;
using Xunit;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public partial class MovementTrafficTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedMovement_RestoresMixedSnapshotsAfterAnEarlierDatagramIsLost(bool scoped)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var sender = new MovementPacketCompressor(serializer);
            var receiver = new MovementPacketCompressor(serializer);
            var capture = CreateEncodeOnceCaptures(mock, riders: 72, mounts: 24, frames: 2);
            var skipped = capture[0].RiderBatches[1];
            var latest = capture[1].RiderBatches[1];
            const int count = 18;
            Guid[] canonicalIds = latest.CanonicalIds.Take(count).Reverse().ToArray();
            AgentData[] riders = latest.Data.Take(count).Reverse().ToArray();
            Guid[] deckShips = IndexEncodeOnceDecks(riders)!;
            Assert.Equal(2, deckShips.Length);
            Assert.Contains(riders, rider => rider.MountData != null);
            Assert.Contains(riders, rider => rider.MountData == null);
            long[] revisions = latest.AuthorityRevisions.Take(count).Reverse().ToArray();
            var movement = scoped
                ? new MovementPacket(EquivalenceScope, latest.CompactIds.Take(count).Reverse().ToArray(),
                    riders, EquivalenceSender, revisions, 208, deckShips)
                : new MovementPacket(canonicalIds, riders, EquivalenceSender, revisions, 208, deckShips);

            AgentData[] skippedRiders = skipped.Data.Take(count).ToArray();
            sender.SerializeSnapshots(new MovementPacket(
                skipped.CanonicalIds.Take(count).ToArray(), skippedRiders, EquivalenceSender, sampleSequence: 207,
                deckShips: IndexEncodeOnceDecks(skippedRiders)!), skipped.CanonicalIds.Take(count).ToArray());
            byte[] wire = sender.SerializeSnapshots(movement, canonicalIds);

            var restored = Assert.IsType<MovementPacket>(RestorePackedMovement(serializer, receiver, wire));
            Assert.Equal(serializer.Serialize(movement), serializer.Serialize(restored));
            Assert.Equal(DeliveryMethod.Unreliable, restored.DeliveryMethod);
            for (int i = 0; i < count; i++)
            {
                AgentData snapshot = restored.Agents[i];
                snapshot.ResolveDeckShip(restored.DeckShips);
                Assert.Equal(riders[i].DeckShip, snapshot.DeckShip);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedMovement_ReducesFinalWireBytesForEighteenSnapshots(bool standaloneMounts)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            const int count = 18;
            var ids = new ushort[count];
            var guids = new Guid[count];
            var revisions = new long[count];
            var riders = new AgentData[count];
            var mounts = new AgentMountData[count];
            for (int i = 0; i < count; i++)
            {
                var agent = standaloneMounts ? mock.SpawnMount() : SpawnRider(mock);
                Assert.True(AgentMirror.TryGet(agent, out var mirror));
                PopulatePackedMovementState(mirror, i);
                ids[i] = (ushort)(173 - (i * 3));
                guids[i] = EquivalenceGuid(ids[i]);
                revisions[i] = i * 17L;
                if (standaloneMounts)
                {
                    mounts[i] = new AgentMountData(agent, ids[i], i % 3 == 0 ? "mount-scope-é" : null,
                        i % 3 == 1 ? guids[i] : Guid.Empty, mountAction0Speed: 1.2f,
                        mountAction0IsLocomotion: i % 2 == 0, mountAction0TurnDirection: i % 3 - 1,
                        mountAction0TurnActionIndex: i + 100, mountAction0TurnProgress: 0.42f,
                        mountAction0IsSyntheticTurn: i % 4 == 0, mountAuthorityRevision: i % 2 == 0 ? revisions[i] : null);
                }
                else
                {
                    riders[i] = new AgentData(agent);
                }
            }

            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var legacy = new LegacyMovementPacketCompressor(serializer);
            var compressor = new MovementPacketCompressor(serializer);
            IPacket packet = standaloneMounts
                ? new MountMovementPacket(guids, mounts, EquivalenceSender, revisions, 211)
                : new MovementPacket(EquivalenceScope, ids, riders, EquivalenceSender, revisions, 211);
            byte[] baseline = legacy.Serialize(packet);
            byte[] wire = compressor.Serialize(packet);
            output.WriteLine($"{packet.PacketType}, {count} snapshots: {baseline.Length} baseline bytes, {wire.Length} packed bytes");
            Assert.IsType<CompressedMovementPacket>(serializer.Deserialize<IPacket>(baseline));
            Assert.True(wire.Length < baseline.Length, $"Packed datagram was {wire.Length} B, baseline {baseline.Length} B.");
            IPacket restored = RestorePackedMovement(serializer, compressor, wire);
            Assert.Equal(serializer.Serialize(packet), serializer.Serialize(restored));
        });
    }

    private static IPacket RestorePackedMovement(
        ProtoBufSerializer serializer, MovementPacketCompressor compressor, byte[] wire)
    {
        var envelope = Assert.IsType<CompressedMovementPacket>(serializer.Deserialize<IPacket>(wire));
        Assert.Equal(wire.Length, compressor.GetSerializedLength(envelope));
        var body = new byte[envelope.UncompressedLength];
        Assert.Equal(body.Length, LZ4Codec.Decode(envelope.Payload, 0, envelope.Payload.Length, body, 0, body.Length));
        switch (serializer.Deserialize<IPacket>(body))
        {
            case MovementPacket movement:
                Assert.NotEmpty(movement.PackedSnapshots);
                Assert.Null(movement.Agents);
                break;
            case MountMovementPacket mounts:
                Assert.NotEmpty(mounts.PackedSnapshots);
                Assert.Null(mounts.Mounts);
                break;
            default:
                throw new InvalidOperationException("Expected the envelope to carry movement snapshots.");
        }
        Assert.True(compressor.TryRestore(envelope, out IPacket restored));
        return restored;
    }

    private static void PopulatePackedMovementState(MirrorAgent mirror, int seed)
    {
        PopulateMovementState(mirror, seed + 1);
        var random = new Random(1289 + seed);
        float Jitter(float value) => value + ((float)random.NextDouble() * 0.02f);
        mirror.Position = new Vec3(Jitter(433f), Jitter(-105f), Jitter(15f));
        mirror.LookDirection = new Vec3(Jitter(0.93f), Jitter(-0.22f), Jitter(0.18f));
        mirror.InputVector = new Vec2(Jitter(0.37f), Jitter(-0.91f));
        mirror.MovementDirection = new Vec2(Jitter(0.92f), Jitter(-0.17f));
        mirror.RealGlobalVelocity = new Vec3(Jitter(3.2f), Jitter(-0.7f), 0f);
        mirror.Action0Progress = Jitter(0.42f);
        mirror.Action1Progress = Jitter(0.75f);
        mirror.Action1Index = 1234 + seed;
    }
}
