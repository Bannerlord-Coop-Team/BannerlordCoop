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
            const int count = 18;
            var deckShips = new[] { EquivalenceGuid(9001), EquivalenceGuid(9002) };
            var canonicalIds = new Guid[count];
            var compactIds = new ushort[count];
            var revisions = new long[count];
            var first = new AgentData[count];
            var latest = new AgentData[count];
            for (int i = 0; i < count; i++)
            {
                var rider = SpawnRider(mock);
                var mount = i % 3 == 0 ? mock.SpawnMount(rider) : null;
                Assert.True(AgentMirror.TryGet(rider, out var mirror));
                if (mount != null)
                {
                    Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
                    PopulatePackedMovementState(mountMirror, i + count);
                }
                canonicalIds[i] = EquivalenceGuid(i);
                compactIds[i] = (ushort)(i + 1);
                revisions[i] = i + 1;
                PopulatePackedMovementState(mirror, i);
                first[i] = new AgentData(rider);
                PopulatePackedMovementState(mirror, i + (2 * count));
                latest[i] = mount == null
                    ? new AgentData(rider)
                    : new AgentData(rider, (ushort)(2000 + i), EquivalenceScope, EquivalenceGuid(2000 + i),
                        mountAuthorityRevision: i);
                if (i % 5 == 0)
                {
                    int hull = i % deckShips.Length;
                    latest[i].CaptureDeck(deckShips[hull], new Vec3(i * 0.25f, -i * 0.5f, 0.75f), 1.5f);
                    latest[i].IndexDeck(hull + 1);
                }
            }

            sender.Serialize(new MovementPacket(canonicalIds, first, EquivalenceSender, sampleSequence: 207));
            Array.Reverse(latest);
            Array.Reverse(compactIds);
            Array.Reverse(canonicalIds);
            Array.Reverse(revisions);
            var movement = scoped
                ? new MovementPacket(EquivalenceScope, compactIds, latest, EquivalenceSender, revisions, 208, deckShips)
                : new MovementPacket(canonicalIds, latest, EquivalenceSender, revisions, 208, deckShips);
            byte[] wire = sender.Serialize(movement);

            var restored = Assert.IsType<MovementPacket>(RestorePackedMovement(serializer, receiver, wire));
            Assert.Equal(serializer.Serialize(movement), serializer.Serialize(restored));
            Assert.Equal(DeliveryMethod.Unreliable, restored.DeliveryMethod);
            for (int i = 0; i < count; i++)
            {
                AgentData snapshot = restored.Agents[i];
                snapshot.ResolveDeckShip(restored.DeckShips);
                Assert.Equal(latest[i].DeckShip, snapshot.DeckShip);
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
                    // Exercise both sides of the length-prefix boundary in UTF-8 bytes.
                    string? scope = i switch
                    {
                        0 => new string('é', 63) + "a",
                        3 => new string('é', 62) + "😀",
                        6 => string.Empty,
                        _ => i % 3 == 0 ? "mount-scope-é" : null,
                    };
                    mounts[i] = new AgentMountData(agent, ids[i], scope,
                        i % 3 == 1 ? guids[i] : Guid.Empty, mountAction0Speed: 1.2f,
                        mountAction0IsLocomotion: i % 2 == 0, mountAction0TurnDirection: (i % 3) - 1,
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
