using Common.Messaging;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// Has GameInterface remove a deployed siege engine from its slot on this client.
/// </summary>
public record ChangeSiegeEngineUndeployed : ICommand
{
    public string ContainerId { get; }
    public int Index { get; }
    public bool IsRanged { get; }
    public bool MoveToReserve { get; }

    public ChangeSiegeEngineUndeployed(string containerId, int index, bool isRanged, bool moveToReserve)
    {
        ContainerId = containerId;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
    }
}
