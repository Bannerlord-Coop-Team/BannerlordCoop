using Common.Messaging;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// Has GameInterface add a prebuilt siege engine to a container's reserve on this client.
/// </summary>
public record ChangeSiegeEngineReserveAdded : ICommand
{
    public string ContainerId { get; }
    public string SiegeEngineId { get; }
    public string EngineTypeId { get; }

    public ChangeSiegeEngineReserveAdded(string containerId, string siegeEngineId, string engineTypeId)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
        EngineTypeId = engineTypeId;
    }
}
