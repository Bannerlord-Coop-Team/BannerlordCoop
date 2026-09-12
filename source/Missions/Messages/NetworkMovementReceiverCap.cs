using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>Advertises the highest bulk movement rate this mission member can currently receive.</summary>
[ProtoContract]
public readonly struct NetworkMovementReceiverCap : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly int MaximumBulkHz;

    [ProtoMember(3)]
    public readonly long Sequence;

    [ProtoMember(4)]
    public readonly double MaximumIncomingMovementBytesPerSecondPerSender;

    [ProtoMember(5)]
    public readonly System.Guid FocusAgentId;

    public NetworkMovementReceiverCap(
        string controllerId,
        int maximumBulkHz,
        long sequence)
        : this(
            controllerId,
            maximumBulkHz,
            sequence,
            1024 * 1024,
            System.Guid.Empty)
    {
    }

    public NetworkMovementReceiverCap(
        string controllerId,
        int maximumBulkHz,
        long sequence,
        double maximumIncomingMovementBytesPerSecondPerSender,
        System.Guid focusAgentId)
    {
        ControllerId = controllerId;
        MaximumBulkHz = maximumBulkHz;
        Sequence = sequence;
        MaximumIncomingMovementBytesPerSecondPerSender =
            maximumIncomingMovementBytesPerSecondPerSender;
        FocusAgentId = focusAgentId;
    }
}
