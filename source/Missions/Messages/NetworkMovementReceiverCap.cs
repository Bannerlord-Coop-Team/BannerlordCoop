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

    public NetworkMovementReceiverCap(
        string controllerId,
        int maximumBulkHz,
        long sequence)
    {
        ControllerId = controllerId;
        MaximumBulkHz = maximumBulkHz;
        Sequence = sequence;
    }
}
