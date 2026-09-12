using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// A deployed siege engine was removed from its slot on the server.
/// </summary>
public readonly struct SiegeEngineUndeployed : IEvent
{
    public readonly SiegeEnginesContainer Container;
    public readonly int Index;
    public readonly bool IsRanged;
    public readonly bool MoveToReserve;

    public SiegeEngineUndeployed(SiegeEnginesContainer container, int index, bool isRanged, bool moveToReserve)
    {
        Container = container;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
    }
}
