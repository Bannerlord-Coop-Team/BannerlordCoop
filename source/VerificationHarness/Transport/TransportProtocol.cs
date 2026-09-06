using ProtoBuf;

namespace VerificationHarness.Transport;

public enum TransportMessageKind
{
    Invalid = 0,
    Hello = 1,
    State = 2,
    Acknowledgement = 3,
    Goodbye = 4,
    Rejection = 5,
    Shutdown = 6,
    ShutdownAcknowledged = 7
}

[ProtoContract]
public sealed class TransportEnvelope
{
    [ProtoMember(1)]
    public int ProtocolVersion { get; set; }

    [ProtoMember(2)]
    public string InstanceId { get; set; } = string.Empty;

    [ProtoMember(3)]
    public int Generation { get; set; }

    [ProtoMember(4)]
    public long Sequence { get; set; }

    [ProtoMember(5)]
    public TransportMessageKind Kind { get; set; }

    [ProtoMember(6)]
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}

[ProtoContract]
public sealed class TransportHelloPayload
{
    [ProtoMember(1)]
    public string Role { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class TransportStatePayload
{
    [ProtoMember(1)]
    public int StateVersion { get; set; }

    [ProtoMember(2)]
    public string Marker { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class TransportAcknowledgementPayload
{
    [ProtoMember(1)]
    public string Digest { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class TransportGoodbyePayload
{
    [ProtoMember(1)]
    public string Reason { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class TransportRejectionPayload
{
    [ProtoMember(1)]
    public string Code { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Detail { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class TransportShutdownPayload
{
    [ProtoMember(1)]
    public string Reason { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class TransportShutdownAcknowledgedPayload
{
    [ProtoMember(1)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class TransportStateSnapshot
{
    public int ProtocolVersion { get; }
    public int StateVersion { get; }
    public string Marker { get; }

    public TransportStateSnapshot(int protocolVersion, int stateVersion, string marker)
    {
        ProtocolVersion = protocolVersion;
        StateVersion = stateVersion;
        Marker = marker;
    }
}
