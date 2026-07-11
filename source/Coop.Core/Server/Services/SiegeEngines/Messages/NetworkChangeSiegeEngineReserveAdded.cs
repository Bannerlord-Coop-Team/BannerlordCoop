using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients a prebuilt siege engine was added to a container's reserve.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSiegeEngineReserveAdded : IEvent
{
    [ProtoMember(1)]
    public string ContainerId { get; }
    [ProtoMember(2)]
    public string SiegeEngineId { get; }
    [ProtoMember(3)]
    public string EngineTypeId { get; }

    public NetworkChangeSiegeEngineReserveAdded(string containerId, string siegeEngineId, string engineTypeId)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
        EngineTypeId = engineTypeId;
    }
}
