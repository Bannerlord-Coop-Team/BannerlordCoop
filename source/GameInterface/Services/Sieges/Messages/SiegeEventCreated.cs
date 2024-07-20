using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Sieges.Messages;
internal class SiegeEventCreated : IEvent
{
    public SiegeEvent Instance { get; }

    public SiegeEventCreated(SiegeEvent instance)
    {
        Instance = instance;
    }
}
