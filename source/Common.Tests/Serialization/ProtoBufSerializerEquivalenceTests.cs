using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Voice;
using LiteNetLib;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Common.Tests.Serialization;

/// <summary>
/// Pins <see cref="ProtoBufSerializer.Serialize"/> to the bytes the pre-#3076 serializer wrote.
/// Movement batching sizes candidates by exact wire length, so semantic round trips are not enough.
/// </summary>
public class ProtoBufSerializerEquivalenceTests
{
    private static readonly SerializableTypeMapper TypeMapper = new SerializableTypeMapper();
    private static readonly LegacyProtoBufSerializer Legacy = new LegacyProtoBufSerializer(TypeMapper);
    private static readonly ProtoBufSerializer Current = new ProtoBufSerializer(TypeMapper);

    public static IEnumerable<object[]> PayloadNames => CreatePayloads().Select(payload => new object[] { payload.Key });

    public static IEnumerable<object[]> ForcedTypeIds => new[]
    {
        0, 1, 127, 128, 16383, 16384, 2097151, 2097152, 268435455, 268435456, int.MaxValue, -1, int.MinValue,
    }.Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(PayloadNames))]
    public void Serialize_WritesLegacyBytes(string payloadName)
    {
        object payload = CreatePayloads()[payloadName];

        byte[] expected = Legacy.Serialize(payload);
        byte[] actual = Current.Serialize(payload);

        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(PayloadNames))]
    public void Deserialize_ReadsLegacyBytesWithEveryValue(string payloadName)
    {
        byte[] legacyBytes = Legacy.Serialize(CreatePayloads()[payloadName]);

        object restored = Current.Deserialize(legacyBytes);

        // Same bytes after the round trip means every serialized member kept its value.
        Assert.Equal(legacyBytes, Legacy.Serialize(restored));
    }

    [Theory]
    [MemberData(nameof(PayloadNames))]
    public void LegacyReader_ReadsCurrentBytesWithEveryValue(string payloadName)
    {
        byte[] currentBytes = Current.Serialize(CreatePayloads()[payloadName]);

        object restored = Legacy.Deserialize(currentBytes);

        Assert.Equal(currentBytes, Current.Serialize(restored));
    }

    [Theory]
    [MemberData(nameof(ForcedTypeIds))]
    public void Serialize_WritesLegacyHeaderForForcedTypeId(int typeId)
    {
        var mapper = new FixedIdTypeMapper(typeId);
        var legacy = new LegacyProtoBufSerializer(mapper);
        var current = new ProtoBufSerializer(mapper);

        foreach (object payload in new object[] { new EmptyContract(), SmallMessage() })
        {
            byte[] expected = legacy.Serialize(payload);

            Assert.Equal(expected, current.Serialize(payload));
            Assert.Equal(typeId, LegacyProtoBufSerializer.ReadWrapper(expected).TypeId);
        }
    }

    [Theory]
    [InlineData(0, "12-00")]
    [InlineData(1, "08-01-12-00")]
    [InlineData(127, "08-7F-12-00")]
    [InlineData(128, "08-80-01-12-00")]
    [InlineData(16384, "08-80-80-01-12-00")]
    [InlineData(268435455, "08-FF-FF-FF-7F-12-00")]
    [InlineData(268435456, "08-80-80-80-80-01-12-00")]
    [InlineData(int.MaxValue, "08-FF-FF-FF-FF-07-12-00")]
    [InlineData(-1, "08-FF-FF-FF-FF-FF-FF-FF-FF-FF-01-12-00")]
    [InlineData(int.MinValue, "08-80-80-80-80-F8-FF-FF-FF-FF-01-12-00")]
    public void Serialize_WritesGoldenHeaderForEmptyPayload(int typeId, string expectedHex)
    {
        var current = new ProtoBufSerializer(new FixedIdTypeMapper(typeId));

        Assert.Equal(expectedHex, BitConverter.ToString(current.Serialize(new EmptyContract())));
    }

    [Fact]
    public void Serialize_WritesGoldenBytesForMappedValueTypeMessage()
    {
        // Pins the stable type id and the field framing without the legacy copy.
        byte[] actual = Current.Serialize(new NetworkConnectedPlayersChanged(3));

        Assert.Equal(GoldenConnectedPlayersChangedHex, BitConverter.ToString(actual));
    }

    [Theory]
    [InlineData(125, 127)]
    [InlineData(126, 128)]
    [InlineData(16380, 16383)]
    [InlineData(16381, 16384)]
    [InlineData(2097147, 2097151)]
    [InlineData(2097148, 2097152)]
    public void Serialize_WritesLegacyLengthPrefixAtVarintBoundary(int blobLength, int payloadLength)
    {
        var payload = new BlobContract { Blob = Pattern(blobLength) };

        byte[] expected = Legacy.Serialize(payload);

        Assert.Equal(payloadLength, LegacyProtoBufSerializer.ReadWrapper(expected).Payload.Length);
        Assert.Equal(expected, Current.Serialize(payload));
    }

    [Fact]
    public void Serialize_NullThrowsLikeLegacy()
    {
        var expected = Assert.ThrowsAny<Exception>(() => Legacy.Serialize(null!));
        var actual = Assert.ThrowsAny<Exception>(() => Current.Serialize(null!));

        Assert.IsType<NullReferenceException>(expected);
        Assert.Equal(expected.GetType(), actual.GetType());
    }

    [Fact]
    public void Serialize_UnmappedTypeThrowsLikeLegacy()
    {
        var mapper = new FixedIdTypeMapper(typeId: 1, mapsTypes: false);

        var expected = Assert.Throws<InvalidOperationException>(() => new LegacyProtoBufSerializer(mapper).Serialize(SmallMessage()));
        var actual = Assert.Throws<InvalidOperationException>(() => new ProtoBufSerializer(mapper).Serialize(SmallMessage()));

        Assert.Equal(expected.Message, actual.Message);
    }

    [Fact]
    public void Serialize_NonContractTypeThrowsLikeLegacy()
    {
        var expected = Assert.ThrowsAny<Exception>(() => Legacy.Serialize(new NotAContract()));
        var actual = Assert.ThrowsAny<Exception>(() => Current.Serialize(new NotAContract()));

        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
    }

    [Fact]
    public void Serialize_ThrowingMemberThrowsLikeLegacyAndNextCallStillMatches()
    {
        var expected = Assert.ThrowsAny<Exception>(() => Legacy.Serialize(new ThrowingContract()));
        var actual = Assert.ThrowsAny<Exception>(() => Current.Serialize(new ThrowingContract()));

        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        object next = CreatePayloads()["movement compact ids"];
        Assert.Equal(Legacy.Serialize(next), Current.Serialize(next));
    }

    [Fact]
    public void Serialize_ValueChangingDuringWriteMatchesLegacy()
    {
        ShiftingContract.Calls = 0;
        byte[] expected = Legacy.Serialize(new ShiftingHolder());
        ShiftingContract.Calls = 0;
        byte[] actual = Current.Serialize(new ShiftingHolder());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Serialize_ReentrantCallMatchesLegacy()
    {
        ReentrantContract.Serialize = Legacy.Serialize;
        byte[] expected = Legacy.Serialize(new ReentrantContract());
        ReentrantContract.Serialize = Current.Serialize;
        byte[] actual = Current.Serialize(new ReentrantContract());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Serialize_ConcurrentCallsMatchLegacy()
    {
        var payloads = CreatePayloads().Values.ToArray();
        // Registers every type first; the mapper is not written to concurrently after this.
        byte[][] expected = payloads.Select(Legacy.Serialize).ToArray();
        int mismatches = 0;

        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, worker =>
        {
            for (int i = 0; i < 400; i++)
            {
                int index = (worker + i) % payloads.Length;
                if (!Current.Serialize(payloads[index]).AsSpan().SequenceEqual(expected[index]))
                    Interlocked.Increment(ref mismatches);
            }
        });

        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void Serialize_RoundTripsRealPacketValues()
    {
        var relay = new RelayPacket(DeliveryMethod.Sequenced, "MapEvent_Created_0000", "76561198000000042", Pattern(300));
        var voice = (VoiceContextChanged)CreatePayloads()["small message"];

        var restoredRelay = Current.Deserialize<RelayPacket>(Current.Serialize(relay));
        var restoredVoice = Current.Deserialize<VoiceContextChanged>(Current.Serialize(voice));
        var restoredPlayers = Current.Deserialize<NetworkConnectedPlayersChanged>(
            Current.Serialize(new NetworkConnectedPlayersChanged(12)));

        Assert.Equal(relay.DeliveryMethod, restoredRelay.DeliveryMethod);
        Assert.Equal(relay.InstanceId, restoredRelay.InstanceId);
        Assert.Equal(relay.ControllerId, restoredRelay.ControllerId);
        Assert.Equal(relay.Payload, restoredRelay.Payload);
        Assert.Equal(voice.SentAt, restoredVoice.SentAt);
        Assert.Equal(voice.Position.Context, restoredVoice.Position.Context);
        Assert.Equal(voice.Position.X, restoredVoice.Position.X);
        Assert.Equal(voice.Position.CanHear, restoredVoice.Position.CanHear);
        Assert.Equal(12, restoredPlayers.ConnectedPlayers);
    }

    [Fact]
    public void Serialize_PayloadDecodesWithMonoConfiguredModel()
    {
        var expected = new MonoSensitiveStruct(42, "linux", -3.5f);
        var model = RuntimeTypeModel.Create();
        ProtoBufSerializer.ConfigureRuntimeModel(model, isMonoRuntime: true);

        byte[] payload = LegacyProtoBufSerializer.ReadWrapper(Current.Serialize(expected)).Payload;
        using var stream = new MemoryStream(payload);
        var actual = (MonoSensitiveStruct)model.Deserialize(stream, value: null, typeof(MonoSensitiveStruct));

        Assert.True(model[typeof(MonoSensitiveStruct)].UseConstructor);
        Assert.Equal(expected.Number, actual.Number);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.Scale, actual.Scale);
    }

    // Type id 919165603 (FNV-1a of the full name) then the payload 08-03, as the pre-#3076 serializer wrote it.
    private const string GoldenConnectedPlayersChangedHex = "08-A3-B5-A5-B6-03-12-02-08-03";

    private static Dictionary<string, object> CreatePayloads()
    {
        var connectedPlayers = new NetworkConnectedPlayersChanged(12);
        Legacy.Serialize(connectedPlayers);

        return new Dictionary<string, object>
        {
            ["empty contract"] = new EmptyContract(),
            ["small message"] = new VoiceContextChanged
            {
                SentAt = 1234567890123L,
                Position = new VoicePosition(VoicePosition.CampaignContext, 7, 1.5f, -2.25f, 3f, true, false),
            },
            ["value type message"] = connectedPlayers,
            ["message packet"] = MessagePacket.Create(connectedPlayers, Legacy),
            ["relay packet"] = new RelayPacket(DeliveryMethod.Unreliable, "MapEvent_Created_0000", "76561198000000042", Pattern(200)),
            ["voice packet"] = new VoicePacket
            {
                Position = new VoicePosition("MapEvent_Created_0000", 3, 10f, 20f, 30f, true, true),
                Sequence = 77,
                Audio = Pattern(VoicePacket.MaximumPayload),
                Speaker = "76561198000000042",
                ListenerEpoch = 5,
                Gain = 0.8f,
                StateSequence = 9,
                SentAt = 638000000000000000L,
                StreamGeneration = 2,
            },
            ["movement compact ids"] = MovementShape(60, compactIds: true),
            ["movement guid fallback"] = MovementShape(60, compactIds: false),
            ["movement single agent"] = MovementShape(1, compactIds: true),
            ["compressed movement envelope"] = new CompressedMovementShape(2048, Pattern(700)),
            ["aggregate batch"] = new AggregateMessagePacket(Enumerable.Range(0, 200)
                .Select(i => Legacy.Serialize(new NetworkConnectedPlayersChanged(i)))
                .ToArray()),
            ["large blob"] = new BlobContract
            {
                Blob = Pattern(200_000),
                Strings = Enumerable.Range(0, 1000).Select(i => "string-number-" + i).ToList(),
            },
            ["over one megabyte"] = new BlobContract { Blob = Pattern(2 * 1024 * 1024) },
            ["empty arrays"] = new EmptyArrays(),
            ["empty aggregate"] = new AggregateMessagePacket(Array.Empty<byte[]>()),
            ["null optionals"] = new NullsAndDefaults(),
            ["null relay fields"] = new RelayPacket(DeliveryMethod.Unreliable, null!, null!, null!),
            ["default values"] = new NullsAndDefaults
            {
                OptionalText = string.Empty,
                Numbers = Array.Empty<int>(),
                Bytes = Array.Empty<byte>(),
                Child = new NestedLeaf(),
                OptionalRevision = 0,
            },
            ["default value type"] = new NetworkConnectedPlayersChanged(0),
            ["mono sensitive struct"] = new MonoSensitiveStruct(42, "linux", -3.5f),
            ["nested"] = new NestedRoot
            {
                Value = 1,
                Child = new NestedMiddle { Value = 2, Child = new NestedLeaf { Value = 3, Text = "deep" } },
                Children = Enumerable.Range(0, 10)
                    .Select(i => new NestedMiddle { Value = i, Child = new NestedLeaf { Value = i * 2, Text = "leaf" + i } })
                    .ToList(),
            },
            ["unicode"] = new NestedLeaf { Value = -1, Text = "Çalışkan ğüşiöç – 日本語テキスト 😀 Ωμέγα ẞ" },
            ["long tail string"] = new NestedLeaf { Value = 1, Text = string.Concat(Enumerable.Repeat("ğ日😀a", 700)) },
        };
    }

    private static VoiceContextChanged SmallMessage() =>
        new VoiceContextChanged { SentAt = 99, Position = new VoicePosition("campaign", 1, 1f, 2f, 3f, true, true) };

    private static MovementShapePacket MovementShape(int agents, bool compactIds)
    {
        var data = Enumerable.Range(0, agents).Select(i => new MovementShapeAgent(
            new ShapeVec3(100.5f + i, 200.25f - i, 3.125f * i),
            new ShapeVec2(0.75f, -0.5f),
            new ShapeVec3(0.7071f, -0.7071f, 0f),
            new ShapeVec2(-0.75f, 0.5f),
            4.5f + (i % 7),
            (uint)(i % 5))).ToArray();
        long[] revisions = Enumerable.Range(0, agents).Select(i => (long)i * 3).ToArray();

        return compactIds
            ? new MovementShapePacket("MapEvent_Created_0000", Enumerable.Range(1, agents).Select(i => (ushort)i).ToArray(), data, null, "76561198000000042", revisions, 123456)
            : new MovementShapePacket(null, null, data, Enumerable.Range(0, agents).Select(DeterministicGuid).ToArray(), "76561198000000042", revisions, 123456);
    }

    private static Guid DeterministicGuid(int seed)
    {
        var bytes = new byte[16];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((seed * 31) + (i * 17) + 1);
        return new Guid(bytes);
    }

    private static byte[] Pattern(int length)
    {
        var bytes = new byte[length];
        uint state = 0x9E3779B9u;
        for (int i = 0; i < bytes.Length; i++)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            bytes[i] = (byte)(state >> 24);
        }
        return bytes;
    }

    private sealed class FixedIdTypeMapper : ISerializableTypeMapper
    {
        private readonly int typeId;
        private readonly bool mapsTypes;

        public FixedIdTypeMapper(int typeId, bool mapsTypes = true)
        {
            this.typeId = typeId;
            this.mapsTypes = mapsTypes;
        }

        public void AddTypes(IEnumerable<Type> types)
        {
        }

        public bool TryGetId(Type type, out int id)
        {
            id = mapsTypes ? typeId : 0;
            return mapsTypes;
        }

        public bool TryGetType(int id, out Type type)
        {
            type = null!;
            return false;
        }
    }

    public class NotAContract
    {
        public int Value { get; set; } = 5;
    }

    [ProtoContract]
    public class EmptyContract
    {
    }

    [ProtoContract]
    public class BlobContract
    {
        [ProtoMember(1)]
        public byte[]? Blob { get; set; }
        [ProtoMember(2)]
        public List<string>? Strings { get; set; }
    }

    [ProtoContract]
    public class EmptyArrays
    {
        [ProtoMember(1)]
        public int[] Numbers { get; set; } = Array.Empty<int>();
        [ProtoMember(2, IsPacked = true)]
        public int[] PackedNumbers { get; set; } = Array.Empty<int>();
        [ProtoMember(3)]
        public string[] Texts { get; set; } = Array.Empty<string>();
        [ProtoMember(4)]
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        [ProtoMember(5)]
        public List<NestedLeaf> Leaves { get; set; } = new List<NestedLeaf>();
    }

    [ProtoContract]
    public class NullsAndDefaults
    {
        [ProtoMember(1)]
        public string? OptionalText { get; set; }
        [ProtoMember(2)]
        public int Zero { get; set; }
        [ProtoMember(3)]
        public NestedLeaf? Child { get; set; }
        [ProtoMember(4)]
        public int[]? Numbers { get; set; }
        [ProtoMember(5)]
        public bool Flag { get; set; }
        [ProtoMember(6)]
        public double Ratio { get; set; }
        [ProtoMember(7)]
        public Guid Id { get; set; }
        [ProtoMember(8)]
        public byte[]? Bytes { get; set; }
        [ProtoMember(9)]
        public long? OptionalRevision { get; set; }
    }

    [ProtoContract]
    public class NestedRoot
    {
        [ProtoMember(1)]
        public int Value { get; set; }
        [ProtoMember(2)]
        public NestedMiddle? Child { get; set; }
        [ProtoMember(3)]
        public List<NestedMiddle>? Children { get; set; }
    }

    [ProtoContract]
    public class NestedMiddle
    {
        [ProtoMember(1)]
        public int Value { get; set; }
        [ProtoMember(2)]
        public NestedLeaf? Child { get; set; }
    }

    [ProtoContract]
    public class NestedLeaf
    {
        [ProtoMember(1)]
        public int Value { get; set; }
        [ProtoMember(2)]
        public string? Text { get; set; }
    }

    [ProtoContract(SkipConstructor = true)]
    public readonly struct MonoSensitiveStruct
    {
        [ProtoMember(1)]
        public readonly int Number;
        [ProtoMember(2)]
        public readonly string Text;
        [ProtoMember(3)]
        public readonly float Scale;

        public MonoSensitiveStruct(int number, string text, float scale)
        {
            Number = number;
            Text = text;
            Scale = scale;
        }
    }

    // Same field layout as Vec3Surrogate.
    [ProtoContract]
    public struct ShapeVec3
    {
        [ProtoMember(1, DataFormat = DataFormat.FixedSize)]
        public ulong XY { get; set; }
        [ProtoMember(2)]
        public float Z { get; set; }

        public ShapeVec3(float x, float y, float z)
        {
            XY = (uint)BitConverter.SingleToInt32Bits(x) | ((ulong)(uint)BitConverter.SingleToInt32Bits(y) << 32);
            Z = z;
        }
    }

    // Same field layout as Vec2Surrogate.
    [ProtoContract]
    public struct ShapeVec2
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }

        public ShapeVec2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    // Same field layout as AgentData without the mount.
    [ProtoContract(SkipConstructor = true)]
    public struct MovementShapeAgent
    {
        [ProtoMember(1)]
        public ShapeVec3 Position { get; }
        [ProtoMember(2)]
        public ShapeVec2 InputVector { get; }
        [ProtoMember(3)]
        public ShapeVec3 LookDirection { get; }
        [ProtoMember(4)]
        public ShapeVec2 MovementDirection { get; }
        [ProtoMember(8)]
        public float Speed { get; }
        [ProtoMember(9)]
        public uint MovementFlag { get; }

        public MovementShapeAgent(ShapeVec3 position, ShapeVec2 inputVector, ShapeVec3 lookDirection, ShapeVec2 movementDirection, float speed, uint movementFlag)
        {
            Position = position;
            InputVector = inputVector;
            LookDirection = lookDirection;
            MovementDirection = movementDirection;
            Speed = speed;
            MovementFlag = movementFlag;
        }
    }

    // Same field layout as MovementPacket.
    [ProtoContract]
    public readonly struct MovementShapePacket
    {
        [ProtoMember(1)]
        public string? IdentityScopeId { get; }
        [ProtoMember(2, IsPacked = true)]
        public ushort[]? AgentIds { get; }
        [ProtoMember(3)]
        public MovementShapeAgent[] Agents { get; }
        [ProtoMember(4)]
        public Guid[]? AgentGuids { get; }
        [ProtoMember(5)]
        public string SenderControllerId { get; }
        [ProtoMember(6, IsPacked = true)]
        public long[] AuthorityRevisions { get; }
        [ProtoMember(7)]
        public long SampleSequence { get; }

        public MovementShapePacket(string? identityScopeId, ushort[]? agentIds, MovementShapeAgent[] agents, Guid[]? agentGuids, string senderControllerId, long[] authorityRevisions, long sampleSequence)
        {
            IdentityScopeId = identityScopeId;
            AgentIds = agentIds;
            Agents = agents;
            AgentGuids = agentGuids;
            SenderControllerId = senderControllerId;
            AuthorityRevisions = authorityRevisions;
            SampleSequence = sampleSequence;
        }
    }

    // Same field layout as CompressedMovementPacket.
    [ProtoContract(SkipConstructor = true)]
    public readonly struct CompressedMovementShape
    {
        [ProtoMember(1)]
        public int UncompressedLength { get; }
        [ProtoMember(2)]
        public byte[] Payload { get; }

        public CompressedMovementShape(int uncompressedLength, byte[] payload)
        {
            UncompressedLength = uncompressedLength;
            Payload = payload;
        }
    }

    [ProtoContract]
    public class ThrowingContract
    {
        [ProtoMember(1)]
        public int Before { get; set; } = 7;
        [ProtoMember(2)]
        public List<NestedLeaf> Leaves { get; set; } = Enumerable.Range(0, 20).Select(i => new NestedLeaf { Value = i, Text = "leaf" + i }).ToList();
        [ProtoMember(3)]
        public string Tail
        {
            get => throw new InvalidOperationException("tail getter failed");
            set { }
        }
    }

    [ProtoContract]
    public class ShiftingContract
    {
        [ThreadStatic]
        public static int Calls;

        [ProtoMember(1)]
        public string Text
        {
            get => ++Calls % 2 == 1 ? "short" : "a much longer value than the first one";
            set { }
        }
    }

    [ProtoContract]
    public class ShiftingHolder
    {
        [ProtoMember(1)]
        public int Before { get; set; } = 1;
        [ProtoMember(2)]
        public ShiftingContract Inner { get; set; } = new ShiftingContract();
        [ProtoMember(3)]
        public ShiftingContract Second { get; set; } = new ShiftingContract();
        [ProtoMember(4)]
        public int After { get; set; } = 2;
    }

    [ProtoContract]
    public class ReentrantContract
    {
        [ThreadStatic]
        public static Func<object, byte[]>? Serialize;

        [ProtoMember(1)]
        public int Value { get; set; } = 5;
        [ProtoMember(2)]
        public byte[] Nested
        {
            get => Serialize!(new NestedLeaf { Value = 9, Text = "inner" });
            set { }
        }
    }
}
