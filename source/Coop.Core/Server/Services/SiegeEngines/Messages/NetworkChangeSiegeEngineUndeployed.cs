using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients a deployed siege engine was removed from its slot.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSiegeEngineUndeployed : IEvent
{
    [ProtoMember(1)]
    public string ContainerId { get; }
    [ProtoMember(2)]
    public int Index { get; }
    [ProtoMember(3)]
    public bool IsRanged { get; }
    [ProtoMember(4)]
    public bool MoveToReserve { get; }

    public NetworkChangeSiegeEngineUndeployed(string containerId, int index, bool isRanged, bool moveToReserve)
    {
        ContainerId = containerId;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
    }
}
