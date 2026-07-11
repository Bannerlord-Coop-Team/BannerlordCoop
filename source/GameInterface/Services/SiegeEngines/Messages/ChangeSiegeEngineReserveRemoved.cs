using Common.Messaging;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// Has GameInterface remove a siege engine from a container's reserve on this client.
/// </summary>
public record ChangeSiegeEngineReserveRemoved : ICommand
{
    public string ContainerId { get; }
    public string SiegeEngineId { get; }

    public ChangeSiegeEngineReserveRemoved(string containerId, string siegeEngineId)
    {
        ContainerId = containerId;
        SiegeEngineId = siegeEngineId;
    }
}
