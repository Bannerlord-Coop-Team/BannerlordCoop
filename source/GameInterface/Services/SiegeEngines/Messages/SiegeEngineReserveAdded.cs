using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// A prebuilt siege engine was added to a container's reserve on the server.
/// </summary>
public readonly struct SiegeEngineReserveAdded : IEvent
{
    public readonly SiegeEnginesContainer Container;
    public readonly SiegeEngineConstructionProgress SiegeEngine;

    public SiegeEngineReserveAdded(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine)
    {
        Container = container;
        SiegeEngine = siegeEngine;
    }
}
