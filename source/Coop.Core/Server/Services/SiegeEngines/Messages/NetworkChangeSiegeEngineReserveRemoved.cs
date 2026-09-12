using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients a siege engine was removed from a container's reserve.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSiegeEngineReserveRemoved : IEvent
{
    [ProtoMember(1)]
    public string ContainerId { get; }
    [ProtoMember(2)]
    public string SiegeEngineId { get; }

    public NetworkChangeSiegeEngineReserveRemoved(string containerId, string siegeEngineId)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
    }
}
