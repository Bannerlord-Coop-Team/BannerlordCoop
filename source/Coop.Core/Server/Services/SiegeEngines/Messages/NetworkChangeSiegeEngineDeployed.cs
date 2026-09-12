using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients a siege engine was deployed to a slot.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSiegeEngineDeployed : IEvent
{
    [ProtoMember(1)]
    public string ContainerId { get; }
    [ProtoMember(2)]
    public string SiegeEngineId { get; }
    [ProtoMember(3)]
    public string EngineTypeId { get; }
    [ProtoMember(4)]
    public int Index { get; }
    [ProtoMember(5)]
    public long SlotRevision { get; }
    [ProtoMember(6)]
    public bool IsRanged { get; }
    [ProtoMember(7)]
    public string RevisionEpoch { get; }

    public NetworkChangeSiegeEngineDeployed(
        string containerId,
        string siegeEngineId,
        string engineTypeId,
        int index,
        long slotRevision,
        bool isRanged,
        string revisionEpoch)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
        EngineTypeId = engineTypeId;
        Index = index;
        SlotRevision = slotRevision;
        IsRanged = isRanged;
        RevisionEpoch = revisionEpoch;
    }
}
