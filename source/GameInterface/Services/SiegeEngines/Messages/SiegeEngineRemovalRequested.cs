using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// The local player ordered a deployed siege engine removed from its slot; ask the server to apply it.
/// </summary>
public readonly struct SiegeEngineRemovalRequested : IEvent
{
    public readonly SiegeEvent SiegeEvent;
    public readonly BattleSideEnum Side;
    public readonly int Index;
    public readonly bool IsRanged;
    public readonly bool MoveToReserve;

    public SiegeEngineRemovalRequested(SiegeEvent siegeEvent, BattleSideEnum side, int index, bool isRanged, bool moveToReserve)
    {
        SiegeEvent = siegeEvent;
        Side = side;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
    }
}
