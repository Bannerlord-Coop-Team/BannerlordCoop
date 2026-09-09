using Common.Messaging;
using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace Common.Voice;

[ProtoContract]
public sealed class VoicePacket : IPacket
{
    public const int MaximumPayload = 400;
    public PacketType PacketType => PacketType.Voice;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;
    [ProtoMember(1)] public VoicePosition Position { get; set; }
    [ProtoMember(2)] public uint Sequence { get; set; }
    [ProtoMember(3)] public byte[] Audio { get; set; }
    [ProtoMember(4)] public string Speaker { get; set; }
    [ProtoMember(5)] public long ListenerEpoch { get; set; }
    [ProtoMember(6)] public float Gain { get; set; }
    [ProtoMember(7)] public uint StateSequence { get; set; }
    [ProtoMember(8)] public long SentAt { get; set; }

    [ProtoMember(9)] public long StreamGeneration { get; set; }

    public bool IsValid => Position != null && Position.IsFinite && Position.Epoch > 0 &&
        Position.Context != null && Position.Context.Length <= 256 &&
        Audio != null && Audio.Length <= MaximumPayload &&
        (Speaker == null || Speaker.Length <= 256) && StreamGeneration >= 0;
}

[ProtoContract]
public sealed class VoiceContextChanged : IMessage
{
    [ProtoMember(2)] public long SentAt { get; set; }
    [ProtoMember(1)] public VoicePosition Position { get; set; }
}

[ProtoContract]
public sealed class VoiceConfiguration : IMessage
{
    [ProtoMember(2)] public long SentAt { get; set; }
    [ProtoMember(1)] public VoiceRanges Ranges { get; set; }
}
