using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// The local player ordered a siege engine built/deployed at a slot; ask the server to apply it.
/// </summary>
public readonly struct SiegeEngineDeployRequested : IEvent
{
    public readonly SiegeEvent SiegeEvent;
    public readonly SiegeEnginesContainer Container;
    public readonly BattleSideEnum Side;
    public readonly SiegeEngineType EngineType;
    public readonly int Index;
    public readonly SiegeEngineConstructionProgress ExpectedOccupant;

    public SiegeEngineDeployRequested(
        SiegeEvent siegeEvent,
        SiegeEnginesContainer container,
        BattleSideEnum side,
        SiegeEngineType engineType,
        int index,
        SiegeEngineConstructionProgress expectedOccupant)
    {
        SiegeEvent = siegeEvent;
        Container = container;
        Side = side;
        EngineType = engineType;
        Index = index;
        ExpectedOccupant = expectedOccupant;
    }
}
