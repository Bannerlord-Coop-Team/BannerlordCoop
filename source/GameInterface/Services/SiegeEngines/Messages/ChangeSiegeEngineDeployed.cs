using Common.Messaging;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// Has GameInterface deploy a siege engine to a slot on this client.
/// </summary>
public record ChangeSiegeEngineDeployed : ICommand
{
    public string ContainerId { get; }
    public string SiegeEngineId { get; }
    public string EngineTypeId { get; }
    public int Index { get; }

    public ChangeSiegeEngineDeployed(string containerId, string siegeEngineId, string engineTypeId, int index)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
        EngineTypeId = engineTypeId;
        Index = index;
    }
}
