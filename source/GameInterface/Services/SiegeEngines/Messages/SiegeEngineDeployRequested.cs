using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// The local player ordered a siege engine built/deployed at a slot; ask the server to apply it.
/// </summary>
public readonly struct SiegeEngineDeployRequested : IEvent
{
    public readonly SiegeEvent SiegeEvent;
    public readonly BattleSideEnum Side;
    public readonly SiegeEngineType EngineType;
    public readonly int Index;

    public SiegeEngineDeployRequested(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEngineType engineType, int index)
    {
        SiegeEvent = siegeEvent;
        Side = side;
        EngineType = engineType;
        Index = index;
    }
}
