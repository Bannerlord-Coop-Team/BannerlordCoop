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

    public NetworkChangeSiegeEngineDeployed(string containerId, string siegeEngineId, string engineTypeId, int index)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
        EngineTypeId = engineTypeId;
        Index = index;
    }
}
