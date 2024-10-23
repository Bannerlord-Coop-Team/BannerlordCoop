using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Messages;

internal class SiegeEngineConstructionProgressCreated : IEvent
{
    public SiegeEngineConstructionProgressCreated(SiegeEngineConstructionProgress instance)
    {
        Instance = instance;
    }

    public SiegeEngineConstructionProgress Instance { get; }
}