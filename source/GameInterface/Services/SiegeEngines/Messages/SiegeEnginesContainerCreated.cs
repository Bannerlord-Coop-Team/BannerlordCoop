using Common.Messaging;
using GameInterface.Services.SiegeEngines;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Messages;
internal class SiegeEnginesContainerCreated : IEvent
{
    public SiegeEnginesContainerCreated(SiegeEnginesContainer instance, SiegeEngineConstructionProgress progressInstance)
    {
        SiegeEnginesContainerInstance = instance;
        SiegeEngineConstructionProgressInstance = progressInstance;
    }

    public SiegeEnginesContainer SiegeEnginesContainerInstance { get; }

    public SiegeEngineConstructionProgress SiegeEngineConstructionProgressInstance { get; set; }
}
