using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Voice;
using LiteNetLib;
using ProtoBuf;

namespace Common.Benchmarks;

/// <summary>The #3076 payload list with deterministic contents, shared by the legacy and current runs.</summary>
internal static class BenchmarkPayloads
{
    public static List<(string Name, object Value)> Create(ICommonSerializer serializer)
    {
        var connectedPlayers = new NetworkConnectedPlayersChanged(12);
        serializer.Serialize(connectedPlayers);

        return new List<(string, object)>
        {
            ("empty contract", new EmptyContract()),
            ("small message", new VoiceContextChanged
            {
                SentAt = 1234567890123L,
                Position = new VoicePosition(VoicePosition.CampaignContext, 7, 1.5f, -2.25f, 3f, true, false),
            }),
            ("value type message", connectedPlayers),
            ("message packet", MessagePacket.Create(connectedPlayers, serializer)),
            ("relay packet", new RelayPacket(DeliveryMethod.Unreliable, "MapEvent_Created_0000", "76561198000000042", Pattern(200))),
            ("movement 1 compact", Movement(1, compactIds: true, compressible: false)),
            ("movement 60 compact", Movement(60, compactIds: true, compressible: false)),
            ("movement 60 guid", Movement(60, compactIds: false, compressible: false)),
            ("movement 400 compact", Movement(400, compactIds: true, compressible: false)),
            ("compressed envelope", new CompressedMovementShape(2048, Pattern(700))),
            ("aggregate 200 messages", new AggregateMessagePacket(Enumerable.Range(0, 200)
                .Select(i => serializer.Serialize(new NetworkConnectedPlayersChanged(i)))
                .ToArray())),
            ("blob 200 KB", new BlobContract { Blob = Pattern(200_000) }),
        };
    }

    public static MovementShapePacket Movement(int agents, bool compactIds, bool compressible, int seed = 1)
    {
        MovementShapeAgent[] data = Agents(agents, compressible, seed);
        long[] revisions = Enumerable.Range(0, agents).Select(i => (long)i * 3).ToArray();
        return compactIds
            ? new MovementShapePacket("MapEvent_Created_0000", Enumerable.Range(1, agents).Select(i => (ushort)i).ToArray(), data, null, "76561198000000042", revisions, 123456)
            : new MovementShapePacket(null, null, data, Enumerable.Range(0, agents).Select(DeterministicGuid).ToArray(), "76561198000000042", revisions, 123456);
    }

    public static MovementShapeAgent[] Agents(int count, bool compressible, int seed)
    {
        uint state = unchecked((uint)(seed * 747796405) + 2891336453u);
        var agents = new MovementShapeAgent[count];
        for (int i = 0; i < agents.Length; i++)
        {
            agents[i] = compressible
                ? new MovementShapeAgent(
                    new ShapeVec3(1024.25f, -2048.5f, 512.75f),
                    new ShapeVec2(0.75f, -0.5f),
                    new ShapeVec3(-0.25f, 0.5f, 0.75f),
                    new ShapeVec2(-0.75f, 0.5f),
                    4.5f,
                    9u)
                : new MovementShapeAgent(
                    new ShapeVec3(NextFloat(ref state, 10000f), NextFloat(ref state, 10000f), NextFloat(ref state, 10000f)),
                    new ShapeVec2(NextFloat(ref state, 1f), NextFloat(ref state, 1f)),
                    new ShapeVec3(NextFloat(ref state, 1f), NextFloat(ref state, 1f), NextFloat(ref state, 1f)),
                    new ShapeVec2(NextFloat(ref state, 1f), NextFloat(ref state, 1f)),
                    NextFloat(ref state, 100f),
                    Next(ref state));
        }
        return agents;
    }

    public static Guid DeterministicGuid(int seed)
    {
        var bytes = new byte[16];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((seed * 31) + (i * 17) + 1);
        return new Guid(bytes);
    }

    public static byte[] Pattern(int length)
    {
        var bytes = new byte[length];
        uint state = 0x9E3779B9u;
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(Next(ref state) >> 24);
        return bytes;
    }

    private static float NextFloat(ref uint state, float magnitude) =>
        (((Next(ref state) / (float)uint.MaxValue) * 2f) - 1f) * magnitude;

    private static uint Next(ref uint state)
    {
        state = unchecked((state * 1664525u) + 1013904223u);
        return state;
    }
}

/// <summary>No members, so only the wrapper header is written.</summary>
[ProtoContract]
public class EmptyContract
{
}

/// <summary>One byte array, for the large payload row.</summary>
[ProtoContract]
public class BlobContract
{
    [ProtoMember(1)]
    public byte[]? Blob { get; set; }
}

/// <summary>Same field layout as Vec3Surrogate.</summary>
[ProtoContract]
public struct ShapeVec3
{
    [ProtoMember(1, DataFormat = DataFormat.FixedSize)]
    public ulong XY { get; set; }
    [ProtoMember(2)]
    public float Z { get; set; }

    public ShapeVec3(float x, float y, float z)
    {
        XY = (uint)BitConverter.ToInt32(BitConverter.GetBytes(x), 0) | ((ulong)(uint)BitConverter.ToInt32(BitConverter.GetBytes(y), 0) << 32);
        Z = z;
    }
}

/// <summary>Same field layout as Vec2Surrogate.</summary>
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

/// <summary>Same field layout as AgentData without the mount.</summary>
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

/// <summary>Same field layout as MovementPacket.</summary>
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

/// <summary>Same field layout as CompressedMovementPacket.</summary>
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
