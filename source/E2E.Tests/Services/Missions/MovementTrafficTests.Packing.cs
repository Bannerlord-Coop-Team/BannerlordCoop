using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.MockEngine;
using K4os.Compression.LZ4;
using Missions.Agents.Packets;
using Missions.Services.Network;
using ProtoBuf.Meta;
using TaleWorlds.Library;
using Xunit;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public partial class MovementTrafficTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedMovement_RestoresMixedRiderMountAndDeckSnapshots(bool scoped)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var sender = new MovementPacketCompressor(serializer, new MovementSnapshotPacking());
            var receiver = new MovementPacketCompressor(serializer, new MovementSnapshotPacking());
            MovementPacket movement = CreatePackedMixedRiderPacket(mock, count: 18, scoped);
            byte[] wire = sender.Serialize(movement);

            var restored = Assert.IsType<MovementPacket>(RestorePackedMovement(serializer, receiver, wire));
            Assert.Equal(serializer.Serialize(movement), serializer.Serialize(restored));
        });
    }

    [Fact]
    public void PackedMountMovement_RestoresScopeGuidAndRevisionExtras()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var compressor = new MovementPacketCompressor(serializer, new MovementSnapshotPacking());
            MountMovementPacket packet = CreatePackedMountPacket(mock, count: 18);
            byte[] original = serializer.Serialize(packet);
            byte[] wire = compressor.Serialize(packet);

            var restored = Assert.IsType<MountMovementPacket>(RestorePackedMovement(serializer, compressor, wire));
            Assert.Equal(original, serializer.Serialize(restored));
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
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var compressor = new MovementPacketCompressor(serializer, new MovementSnapshotPacking());
            IPacket packet = standaloneMounts
                ? CreatePackedMountPacket(mock, count)
                : CreatePackedRiderPacket(mock, count);
            byte[] original = serializer.Serialize(packet);
            Assert.InRange(original.Length, 1, MovementPacketCompressor.MaxUncompressedBytes);
            var payload = new byte[LZ4Codec.MaximumOutputSize(original.Length)];
            int compressedLength = LZ4Codec.Encode(
                original, 0, original.Length, payload, 0, payload.Length, LZ4Level.L00_FAST);
            Assert.True(compressedLength > 0);
            int baselineLength = compressor.GetSerializedLength(
                new CompressedMovementPacket(original.Length, payload.AsSpan(0, compressedLength).ToArray()));
            Assert.True(baselineLength < original.Length);

            byte[] wire = compressor.Serialize(packet);
            output.WriteLine($"{packet.PacketType}, {count} snapshots: {baselineLength} baseline bytes, {wire.Length} packed bytes");
            Assert.True(wire.Length < baselineLength, $"Packed datagram was {wire.Length} B, baseline {baselineLength} B.");
        });
    }

    [Fact]
    public void PackedMovement_SchemaCoversProtobufSnapshotFields()
    {
        var model = RuntimeTypeModel.Default;
        Assert.Equal(new[] { 1, 2, 3, 4, 7, 8, 9, 11, 12 },
            model[typeof(AgentData)].GetFields().Select(field => field.FieldNumber).OrderBy(tag => tag));
        Assert.Equal(Enumerable.Range(1, 21),
            model[typeof(AgentMountData)].GetFields().Select(field => field.FieldNumber).OrderBy(tag => tag));
    }

    private static MovementPacket CreatePackedMixedRiderPacket(MockMission mock, int count, bool scoped)
    {
        var deckShips = new[] { EquivalenceGuid(9001), EquivalenceGuid(9002) };
        var canonicalIds = new Guid[count];
        var compactIds = new ushort[count];
        var revisions = new long[count];
        var riders = new AgentData[count];
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
            PopulatePackedMovementState(mirror, i + (2 * count));
            riders[i] = mount == null
                ? new AgentData(rider)
                : new AgentData(rider, (ushort)(2000 + i), EquivalenceScope, EquivalenceGuid(2000 + i),
                    mountAuthorityRevision: i);
            if (i % 5 == 0)
            {
                int hull = i % deckShips.Length;
                riders[i].CaptureDeck(deckShips[hull], new Vec3(i * 0.25f, -i * 0.5f, 0.75f), 1.5f);
                riders[i].IndexDeck(hull + 1);
            }
        }

        // Exercise packet order independently of capture order while keeping ids and revisions aligned.
        Array.Reverse(riders);
        Array.Reverse(compactIds);
        Array.Reverse(canonicalIds);
        Array.Reverse(revisions);
        return scoped
            ? new MovementPacket(EquivalenceScope, compactIds, riders, EquivalenceSender, revisions, 208, deckShips)
            : new MovementPacket(canonicalIds, riders, EquivalenceSender, revisions, 208, deckShips);
    }

    private static MovementPacket CreatePackedRiderPacket(MockMission mock, int count)
    {
        var ids = new ushort[count];
        var revisions = new long[count];
        var riders = new AgentData[count];
        for (int i = 0; i < count; i++)
        {
            var rider = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(rider, out var mirror));
            PopulatePackedMovementState(mirror, i);
            ids[i] = (ushort)(173 - (i * 3));
            revisions[i] = i * 17L;
            riders[i] = new AgentData(rider);
        }

        return new MovementPacket(EquivalenceScope, ids, riders, EquivalenceSender, revisions, 211);
    }

    private static MountMovementPacket CreatePackedMountPacket(MockMission mock, int count)
    {
        // Include both sides of the 127/128 UTF-8 byte boundary and a row without extras.
        var extras = new (string? Scope, Guid MountGuid, long? MountRevision)[]
        {
            (new string('é', 63) + "a", Guid.Empty, null),
            (new string('é', 62) + "😀", EquivalenceGuid(8501), 21L),
            (string.Empty, Guid.Empty, 37L),
            (null, EquivalenceGuid(8502), null),
            (null, Guid.Empty, 0L),
            (null, Guid.Empty, null),
        };
        var guids = new Guid[count];
        var revisions = new long[count];
        var mounts = new AgentMountData[count];
        for (int i = 0; i < count; i++)
        {
            var mount = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(mount, out var mirror));
            PopulatePackedMovementState(mirror, i);
            ushort movementId = (ushort)(173 - (i * 3));
            guids[i] = EquivalenceGuid(movementId);
            revisions[i] = i * 17L;
            (string? scope, Guid mountGuid, long? mountRevision) = extras[i % extras.Length];
            mounts[i] = new AgentMountData(mount, movementId, scope, mountGuid, mountAction0Speed: 1.2f,
                mountAction0IsLocomotion: i % 2 == 0, mountAction0TurnDirection: (i % 3) - 1,
                mountAction0TurnActionIndex: i + 100, mountAction0TurnProgress: 0.42f,
                mountAction0IsSyntheticTurn: i % 4 == 0, mountAuthorityRevision: mountRevision);
        }

        return new MountMovementPacket(guids, mounts, EquivalenceSender, revisions, 211);
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
