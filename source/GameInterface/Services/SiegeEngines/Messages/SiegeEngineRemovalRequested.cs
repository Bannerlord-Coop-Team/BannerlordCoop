using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// The local player ordered a deployed siege engine removed from its slot; ask the server to apply it.
/// </summary>
public readonly struct SiegeEngineRemovalRequested : IEvent
{
    public readonly SiegeEvent SiegeEvent;
    public readonly SiegeEnginesContainer Container;
    public readonly BattleSideEnum Side;
    public readonly int Index;
    public readonly bool IsRanged;
    public readonly bool MoveToReserve;
    public readonly SiegeEngineConstructionProgress ExpectedOccupant;

    public SiegeEngineRemovalRequested(
        SiegeEvent siegeEvent,
        SiegeEnginesContainer container,
        BattleSideEnum side,
        int index,
        bool isRanged,
        bool moveToReserve,
        SiegeEngineConstructionProgress expectedOccupant)
    {
        SiegeEvent = siegeEvent;
        Container = container;
        Side = side;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
        ExpectedOccupant = expectedOccupant;
    }
}
