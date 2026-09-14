using Common.Serialization;
using System.Security.Cryptography;
using VerificationHarness.Serialization;

namespace VerificationHarness.Transport;

public interface ITransportCodec
{
    TransportEncodedFrame Encode<TPayload>(
        string instanceId,
        int generation,
        long sequence,
        TransportMessageKind kind,
        TPayload payload);

    TransportDecodedFrame Decode(byte[] wireBytes);
    string ComputeStateDigest(TransportStatePayload state);
}

public sealed class TransportCodec : ITransportCodec
{
    public const int CurrentProtocolVersion = 1;
    public const string ExpectedLiteNetLibPackageVersion = "1.3.1";

    private readonly ICommonSerializer serializer;
    private readonly ICanonicalJsonHasher canonicalJsonHasher;

    public TransportCodec()
        : this(CreateSerializer(), new CanonicalJsonHasher())
    {
    }

    public TransportCodec(ICommonSerializer serializer, ICanonicalJsonHasher canonicalJsonHasher)
    {
        if (serializer == null) throw new ArgumentNullException(nameof(serializer));
        if (canonicalJsonHasher == null) throw new ArgumentNullException(nameof(canonicalJsonHasher));
        this.serializer = serializer;
        this.canonicalJsonHasher = canonicalJsonHasher;
    }

    public TransportEncodedFrame Encode<TPayload>(
        string instanceId,
        int generation,
        long sequence,
        TransportMessageKind kind,
        TPayload payload)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Instance id is required.", nameof(instanceId));
        if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (kind == TransportMessageKind.Invalid) throw new ArgumentOutOfRangeException(nameof(kind));
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        byte[] payloadBytes = serializer.Serialize(payload);
        var envelope = new TransportEnvelope
        {
            ProtocolVersion = CurrentProtocolVersion,
            InstanceId = instanceId,
            Generation = generation,
            Sequence = sequence,
            Kind = kind,
            Payload = payloadBytes
        };
        byte[] wireBytes = serializer.Serialize(envelope);
        return new TransportEncodedFrame(
            envelope,
            wireBytes,
            Sha256(wireBytes),
            Sha256(payloadBytes));
    }

    public TransportDecodedFrame Decode(byte[] wireBytes)
    {
        if (wireBytes == null) throw new ArgumentNullException(nameof(wireBytes));
        if (wireBytes.Length == 0) throw new InvalidDataException("Transport frame is empty.");

        object value = serializer.Deserialize(wireBytes);
        if (value is not TransportEnvelope envelope)
        {
            throw new InvalidDataException("Transport frame did not contain a transport envelope.");
        }

        if (envelope.Payload == null || envelope.Payload.Length == 0)
        {
            throw new InvalidDataException("Transport envelope payload is empty.");
        }

        object payload = serializer.Deserialize(envelope.Payload);
        Type expectedPayloadType = PayloadTypeFor(envelope.Kind);
        if (!expectedPayloadType.IsInstanceOfType(payload))
        {
            throw new InvalidDataException(
                $"Transport {envelope.Kind} payload was {payload?.GetType().Name ?? "null"}, expected {expectedPayloadType.Name}.");
        }

        return new TransportDecodedFrame(
            envelope,
            payload,
            Sha256(wireBytes),
            Sha256(envelope.Payload));
    }

    public string ComputeStateDigest(TransportStatePayload state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        return canonicalJsonHasher.ComputeSha256(
            new TransportStateSnapshot(CurrentProtocolVersion, state.StateVersion, state.Marker));
    }

    private static ICommonSerializer CreateSerializer()
    {
        var typeMapper = new SerializableTypeMapper();
        typeMapper.AddTypes(new[]
        {
            typeof(TransportEnvelope),
            typeof(TransportHelloPayload),
            typeof(TransportStatePayload),
            typeof(TransportAcknowledgementPayload),
            typeof(TransportGoodbyePayload),
            typeof(TransportRejectionPayload),
            typeof(TransportShutdownPayload),
            typeof(TransportShutdownAcknowledgedPayload)
        });
        return new ProtoBufSerializer(typeMapper);
    }

    private static Type PayloadTypeFor(TransportMessageKind kind)
    {
        return kind switch
        {
            TransportMessageKind.Hello => typeof(TransportHelloPayload),
            TransportMessageKind.State => typeof(TransportStatePayload),
            TransportMessageKind.Acknowledgement => typeof(TransportAcknowledgementPayload),
            TransportMessageKind.Goodbye => typeof(TransportGoodbyePayload),
            TransportMessageKind.Rejection => typeof(TransportRejectionPayload),
            TransportMessageKind.Shutdown => typeof(TransportShutdownPayload),
            TransportMessageKind.ShutdownAcknowledged => typeof(TransportShutdownAcknowledgedPayload),
            _ => throw new InvalidDataException($"Unknown transport message kind: {(int)kind}.")
        };
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

public sealed class TransportEncodedFrame
{
    public TransportEnvelope Envelope { get; }
    public byte[] WireBytes { get; }
    public string WireSha256 { get; }
    public string PayloadSha256 { get; }

    public TransportEncodedFrame(
        TransportEnvelope envelope,
        byte[] wireBytes,
        string wireSha256,
        string payloadSha256)
    {
        Envelope = envelope;
        WireBytes = wireBytes;
        WireSha256 = wireSha256;
        PayloadSha256 = payloadSha256;
    }
}

public sealed class TransportDecodedFrame
{
    public TransportEnvelope Envelope { get; }
    public object Payload { get; }
    public string WireSha256 { get; }
    public string PayloadSha256 { get; }

    public TransportDecodedFrame(
        TransportEnvelope envelope,
        object payload,
        string wireSha256,
        string payloadSha256)
    {
        Envelope = envelope;
        Payload = payload;
        WireSha256 = wireSha256;
        PayloadSha256 = payloadSha256;
    }
}
