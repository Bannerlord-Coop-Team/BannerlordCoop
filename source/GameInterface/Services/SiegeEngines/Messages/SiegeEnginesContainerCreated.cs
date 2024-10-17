using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Messages;

internal class SiegeEnginesContainerCreated : IEvent
{

    public SiegeEnginesContainerCreated(SiegeEnginesContainer instance)
    {
        SiegeEnginesContainerInstance = instance;
    }

    public SiegeEnginesContainer SiegeEnginesContainerInstance { get; }
}