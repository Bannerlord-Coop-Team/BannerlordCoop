using ProtoBuf;
using System.Security.Cryptography;
using System.Text;

namespace VerificationHarness.DedicatedServerSynthetic;

public interface IDedicatedServerWireCodec
{
    DedicatedServerWireFrame DecodeFrame(byte[] wireBytes);
    DedicatedCampaignTime DecodeCampaignTime(byte[] wireBytes);
    DedicatedModuleValidationRequest DecodeModuleValidationRequest(byte[] wireBytes);
    DedicatedModuleValidationResult DecodeModuleValidationResult(byte[] wireBytes);
    string DecodeClientValidationRequest(byte[] wireBytes);
    DedicatedClientValidationResult DecodeClientValidationResult(byte[] wireBytes);
    IReadOnlyList<byte[]> DecodeAggregate(byte[] wireBytes);
    DedicatedSaveChunk DecodeSaveChunk(byte[] wireBytes);
    byte[] EncodeCampaignTime(long serverTicks, int joinPacketsRemaining);
    byte[] EncodeModuleMismatchRequest(string clientBuildVersion);
    byte[] EncodeModuleValidationResult(bool matches, string reason, string serverBuildVersion);
    byte[] EncodeClientValidationRequest(string controllerId);
    byte[] EncodeFreshClientValidationResult();
    byte[] EncodeAggregate(IEnumerable<byte[]> messages);
}

public sealed class DedicatedServerWireCodec : IDedicatedServerWireCodec
{
    public const int MaximumWireBytes = 2 * 1024 * 1024;
    public const int MaximumAggregateMessages = 256;
    public const int MaximumAggregatePayloadBytes = 1024 * 1024;
    public const int MaximumControllerIdBytes = 1024;
    public const int MaximumBuildVersionBytes = 256;
    public const int MaximumReasonBytes = 4096;

    public DedicatedServerWireFrame DecodeFrame(byte[] wireBytes)
    {
        ValidateBytes(wireBytes, MaximumWireBytes, "Wire frame");

        DedicatedWireEnvelope envelope;
        try
        {
            using var stream = new MemoryStream(wireBytes, writable: false);
            envelope = Serializer.Deserialize<DedicatedWireEnvelope>(stream);
        }
        catch (Exception exception) when (exception is ProtoException or EndOfStreamException)
        {
            throw new InvalidDataException("The wire frame is not a valid Common protobuf envelope.", exception);
        }

        if (envelope.TypeId <= 0)
        {
            throw new InvalidDataException("The wire frame has no valid type id.");
        }

        ValidateBytes(envelope.Data, MaximumWireBytes, "Wire payload", allowEmpty: true);
        DedicatedServerWireEntry entry = DedicatedServerWireManifest.GetByTypeId(envelope.TypeId);
        return new DedicatedServerWireFrame(
            entry,
            envelope.Data,
            Hash(wireBytes),
            Hash(envelope.Data));
    }

    public DedicatedCampaignTime DecodeCampaignTime(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.CampaignTimePacketTypeId);
        DedicatedCampaignTimePayload payload = DeserializePayload<DedicatedCampaignTimePayload>(frame.Payload);
        return new DedicatedCampaignTime(payload.ServerTicks, payload.JoinPacketsRemaining);
    }

    public DedicatedModuleValidationRequest DecodeModuleValidationRequest(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.NetworkModuleVersionsValidateTypeId);
        DedicatedModuleValidationRequestPayload payload =
            DeserializePayload<DedicatedModuleValidationRequestPayload>(frame.Payload);
        string buildVersion = RequireBoundedText(
            payload.CoopBuildVersion,
            MaximumBuildVersionBytes,
            "Client build version");
        return new DedicatedModuleValidationRequest(payload.Modules?.Count ?? 0, buildVersion);
    }

    public DedicatedModuleValidationResult DecodeModuleValidationResult(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.NetworkModuleVersionsValidatedTypeId);
        DedicatedModuleValidationResultPayload payload =
            DeserializePayload<DedicatedModuleValidationResultPayload>(frame.Payload);
        string reason = RequireBoundedText(payload.Reason, MaximumReasonBytes, "Validation reason");
        string serverBuildVersion = RequireBoundedText(
            payload.CoopBuildVersion,
            MaximumBuildVersionBytes,
            "Server build version");
        return new DedicatedModuleValidationResult(payload.Matches, reason, serverBuildVersion);
    }

    public string DecodeClientValidationRequest(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.NetworkClientValidateTypeId);
        DedicatedClientValidationRequestPayload payload =
            DeserializePayload<DedicatedClientValidationRequestPayload>(frame.Payload);
        return RequireBoundedText(payload.PlayerId, MaximumControllerIdBytes, "Controller id");
    }

    public DedicatedClientValidationResult DecodeClientValidationResult(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.NetworkClientValidatedTypeId);
        DedicatedClientValidationResultPayload payload =
            DeserializePayload<DedicatedClientValidationResultPayload>(frame.Payload);
        return new DedicatedClientValidationResult(payload.HeroExists, payload.PlayerPayload != null);
    }

    public IReadOnlyList<byte[]> DecodeAggregate(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.AggregateMessagePacketTypeId);
        DedicatedAggregatePayload payload = DeserializePayload<DedicatedAggregatePayload>(frame.Payload);
        byte[][] messages = payload.Messages ?? Array.Empty<byte[]>();
        if (messages.Length == 0 || messages.Length > MaximumAggregateMessages)
        {
            throw new InvalidDataException(
                $"An aggregate must contain between 1 and {MaximumAggregateMessages} messages.");
        }

        long total = 0;
        foreach (byte[] message in messages)
        {
            ValidateBytes(message, MaximumAggregatePayloadBytes, "Aggregate message");
            total += message.Length;
            if (total > MaximumAggregatePayloadBytes)
            {
                throw new InvalidDataException(
                    $"Aggregate payloads cannot exceed {MaximumAggregatePayloadBytes} bytes in total.");
            }

            DedicatedServerWireFrame nestedFrame = DecodeFrame(message);
            if (!IsAllowedAggregateChild(nestedFrame.ManifestEntry.TypeId))
            {
                throw new InvalidDataException(
                    "Aggregate entries must be reliable messages from the safe synthetic subset.");
            }
        }

        return Array.AsReadOnly(messages.Select(x => x.ToArray()).ToArray());
    }

    public DedicatedSaveChunk DecodeSaveChunk(byte[] wireBytes)
    {
        DedicatedServerWireFrame frame = RequireType(
            wireBytes,
            DedicatedServerWireManifest.GameSaveDataChunkPacketTypeId);
        DedicatedSaveChunkPayload payload = DeserializePayload<DedicatedSaveChunkPayload>(frame.Payload);
        return new DedicatedSaveChunk(
            payload.TransferId,
            payload.ChunkIndex,
            payload.ChunkCount,
            payload.CompressedSize,
            payload.UncompressedSize,
            payload.ChunkData ?? Array.Empty<byte>());
    }

    public byte[] EncodeCampaignTime(long serverTicks, int joinPacketsRemaining)
    {
        return Encode(
            DedicatedServerWireManifest.CampaignTimePacketTypeId,
            new DedicatedCampaignTimePayload
            {
                ServerTicks = serverTicks,
                JoinPacketsRemaining = joinPacketsRemaining
            });
    }

    public byte[] EncodeModuleMismatchRequest(string clientBuildVersion)
    {
        clientBuildVersion = RequireBoundedText(
            clientBuildVersion,
            MaximumBuildVersionBytes,
            "Client build version");
        return Encode(
            DedicatedServerWireManifest.NetworkModuleVersionsValidateTypeId,
            new DedicatedModuleValidationRequestPayload
            {
                Modules = new List<DedicatedModuleInfoPayload>(),
                CoopBuildVersion = clientBuildVersion
            });
    }

    public byte[] EncodeModuleValidationResult(bool matches, string reason, string serverBuildVersion)
    {
        reason = RequireBoundedText(reason, MaximumReasonBytes, "Validation reason");
        serverBuildVersion = RequireBoundedText(
            serverBuildVersion,
            MaximumBuildVersionBytes,
            "Server build version");
        return Encode(
            DedicatedServerWireManifest.NetworkModuleVersionsValidatedTypeId,
            new DedicatedModuleValidationResultPayload
            {
                Matches = matches,
                Reason = reason,
                CoopBuildVersion = serverBuildVersion
            });
    }

    public byte[] EncodeClientValidationRequest(string controllerId)
    {
        controllerId = RequireBoundedText(controllerId, MaximumControllerIdBytes, "Controller id");
        return Encode(
            DedicatedServerWireManifest.NetworkClientValidateTypeId,
            new DedicatedClientValidationRequestPayload { PlayerId = controllerId });
    }

    public byte[] EncodeFreshClientValidationResult()
    {
        return Encode(
            DedicatedServerWireManifest.NetworkClientValidatedTypeId,
            new DedicatedClientValidationResultPayload
            {
                HeroExists = false,
                PlayerPayload = null
            });
    }

    public byte[] EncodeAggregate(IEnumerable<byte[]> messages)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        var copy = new List<byte[]>(MaximumAggregateMessages);
        long total = 0;
        foreach (byte[]? message in messages)
        {
            if (copy.Count == MaximumAggregateMessages)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(messages),
                    $"An aggregate cannot contain more than {MaximumAggregateMessages} messages.");
            }

            byte[] messageCopy = message?.ToArray() ?? Array.Empty<byte>();
            ValidateBytes(messageCopy, MaximumAggregatePayloadBytes, "Aggregate message");
            total += messageCopy.Length;
            if (total > MaximumAggregatePayloadBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(messages),
                    $"Aggregate payloads cannot exceed {MaximumAggregatePayloadBytes} bytes in total.");
            }

            DedicatedServerWireFrame nestedFrame = DecodeFrame(messageCopy);
            if (!IsAllowedAggregateChild(nestedFrame.ManifestEntry.TypeId))
            {
                throw new ArgumentException(
                    "Aggregate entries must be reliable messages from the safe synthetic subset.",
                    nameof(messages));
            }

            copy.Add(messageCopy);
        }

        if (copy.Count == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messages),
                "An aggregate must contain at least one message.");
        }

        return Encode(
            DedicatedServerWireManifest.AggregateMessagePacketTypeId,
            new DedicatedAggregatePayload { Messages = copy.ToArray() });
    }

    private DedicatedServerWireFrame RequireType(byte[] wireBytes, int typeId)
    {
        DedicatedServerWireFrame frame = DecodeFrame(wireBytes);
        if (frame.ManifestEntry.TypeId != typeId)
        {
            throw new InvalidDataException(
                $"Expected wire type {typeId}, received {frame.ManifestEntry.TypeId}.");
        }

        return frame;
    }

    private static bool IsAllowedAggregateChild(int typeId) =>
        typeId == DedicatedServerWireManifest.NetworkModuleVersionsValidateTypeId ||
        typeId == DedicatedServerWireManifest.NetworkModuleVersionsValidatedTypeId ||
        typeId == DedicatedServerWireManifest.NetworkClientValidateTypeId ||
        typeId == DedicatedServerWireManifest.NetworkClientValidatedTypeId;

    private static T DeserializePayload<T>(byte[] payload)
    {
        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            return Serializer.Deserialize<T>(stream);
        }
        catch (Exception exception) when (exception is ProtoException or EndOfStreamException)
        {
            throw new InvalidDataException($"The {typeof(T).Name} payload is invalid.", exception);
        }
    }

    private static byte[] Encode<T>(int typeId, T payload)
    {
        byte[] payloadBytes;
        using (var payloadStream = new MemoryStream())
        {
            Serializer.Serialize(payloadStream, payload);
            payloadBytes = payloadStream.ToArray();
        }

        ValidateBytes(payloadBytes, MaximumWireBytes, "Wire payload", allowEmpty: true);
        var envelope = new DedicatedWireEnvelope
        {
            TypeId = typeId,
            Data = payloadBytes
        };
        using var wireStream = new MemoryStream();
        Serializer.Serialize(wireStream, envelope);
        byte[] wireBytes = wireStream.ToArray();
        ValidateBytes(wireBytes, MaximumWireBytes, "Wire frame");
        return wireBytes;
    }

    private static string RequireBoundedText(string? value, int maximumBytes, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} is required.");
        }

        if (Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            throw new InvalidDataException($"{name} exceeds {maximumBytes} UTF-8 bytes.");
        }

        return value;
    }

    private static void ValidateBytes(
        byte[]? bytes,
        int maximumBytes,
        string name,
        bool allowEmpty = false)
    {
        if (bytes == null || (!allowEmpty && bytes.Length == 0))
        {
            throw new InvalidDataException($"{name} is empty.");
        }

        if (bytes.Length > maximumBytes)
        {
            throw new InvalidDataException($"{name} exceeds {maximumBytes} bytes.");
        }
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

public sealed record DedicatedServerWireFrame(
    DedicatedServerWireEntry ManifestEntry,
    byte[] Payload,
    string WireSha256,
    string PayloadSha256);

public sealed record DedicatedCampaignTime(long ServerTicks, int JoinPacketsRemaining);

public sealed record DedicatedModuleValidationRequest(int ModuleCount, string ClientBuildVersion);

public sealed record DedicatedModuleValidationResult(bool Matches, string Reason, string ServerBuildVersion);

public sealed record DedicatedClientValidationResult(bool HeroExists, bool PlayerPayloadPresent);

public sealed record DedicatedSaveChunk(
    int TransferId,
    int ChunkIndex,
    int ChunkCount,
    int CompressedSize,
    int UncompressedSize,
    byte[] ChunkData);

[ProtoContract]
internal sealed class DedicatedWireEnvelope
{
    [ProtoMember(1)]
    public int TypeId { get; set; }

    [ProtoMember(2)]
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

[ProtoContract]
internal sealed class DedicatedCampaignTimePayload
{
    [ProtoMember(1)]
    public long ServerTicks { get; set; }

    [ProtoMember(2)]
    public int JoinPacketsRemaining { get; set; }
}

[ProtoContract]
internal sealed class DedicatedModuleValidationRequestPayload
{
    [ProtoMember(1)]
    public List<DedicatedModuleInfoPayload>? Modules { get; set; }

    [ProtoMember(2)]
    public string? CoopBuildVersion { get; set; }
}

[ProtoContract]
internal sealed class DedicatedModuleInfoPayload
{
    [ProtoMember(1)]
    public string? Id { get; set; }

    [ProtoMember(2)]
    public bool IsOfficial { get; set; }

    [ProtoMember(3)]
    public DedicatedApplicationVersionPayload? Version { get; set; }

    [ProtoMember(4)]
    public bool IsDlc { get; set; }
}

[ProtoContract]
internal sealed class DedicatedApplicationVersionPayload
{
    [ProtoMember(1)]
    public int ApplicationVersionType { get; set; }

    [ProtoMember(2)]
    public int Major { get; set; }

    [ProtoMember(3)]
    public int Minor { get; set; }

    [ProtoMember(4)]
    public int Revision { get; set; }

    [ProtoMember(5)]
    public int ChangeSet { get; set; }
}

[ProtoContract]
internal sealed class DedicatedModuleValidationResultPayload
{
    [ProtoMember(1)]
    public bool Matches { get; set; }

    [ProtoMember(2)]
    public string? Reason { get; set; }

    [ProtoMember(3)]
    public string? CoopBuildVersion { get; set; }
}

[ProtoContract]
internal sealed class DedicatedClientValidationRequestPayload
{
    [ProtoMember(1)]
    public string? PlayerId { get; set; }
}

[ProtoContract]
internal sealed class DedicatedClientValidationResultPayload
{
    [ProtoMember(1)]
    public bool HeroExists { get; set; }

    [ProtoMember(2)]
    public byte[]? PlayerPayload { get; set; }
}

[ProtoContract]
internal sealed class DedicatedAggregatePayload
{
    [ProtoMember(1)]
    public byte[][]? Messages { get; set; }
}

[ProtoContract]
internal sealed class DedicatedSaveChunkPayload
{
    [ProtoMember(1)]
    public int TransferId { get; set; }

    [ProtoMember(2)]
    public int ChunkIndex { get; set; }

    [ProtoMember(3)]
    public int ChunkCount { get; set; }

    [ProtoMember(4)]
    public int CompressedSize { get; set; }

    [ProtoMember(5)]
    public int UncompressedSize { get; set; }

    [ProtoMember(6)]
    public byte[]? ChunkData { get; set; }
}
