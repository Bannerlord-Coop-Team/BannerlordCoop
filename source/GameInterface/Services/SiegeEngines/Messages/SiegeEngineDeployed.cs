using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// A siege engine was deployed to a slot on the server.
/// </summary>
public readonly struct SiegeEngineDeployed : IEvent
{
    public readonly SiegeEnginesContainer Container;
    public readonly SiegeEngineConstructionProgress SiegeEngine;
    public readonly int Index;

    public SiegeEngineDeployed(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine, int index)
    {
        Container = container;
        SiegeEngine = siegeEngine;
        Index = index;
    }
}
