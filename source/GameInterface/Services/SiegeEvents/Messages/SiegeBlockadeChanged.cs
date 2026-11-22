using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Messages;

internal readonly struct SiegeBlockadeChanged : IEvent
{
    public readonly SiegeEvent SiegeEvent;

    public SiegeBlockadeChanged(SiegeEvent siegeEvent)
    {
        SiegeEvent = siegeEvent;
    }
}
