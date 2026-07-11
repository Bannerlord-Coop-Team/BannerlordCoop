using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// A siege engine was removed from a container's reserve on the server.
/// </summary>
public readonly struct SiegeEngineReserveRemoved : IEvent
{
    public readonly SiegeEnginesContainer Container;
    public readonly SiegeEngineConstructionProgress SiegeEngine;

    public SiegeEngineReserveRemoved(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine)
    {
        Container = container;
        SiegeEngine = siegeEngine;
    }
}
