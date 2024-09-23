using Common.Messaging;
using GameInterface.Services.SiegeEngines;
using TaleWorlds.CampaignSystem.Siege;
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
