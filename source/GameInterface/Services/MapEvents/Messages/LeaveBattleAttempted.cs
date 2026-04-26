using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct LeaveBattleAttempted : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly MapEvent MapEvent;

    public LeaveBattleAttempted(MobileParty mobileParty, MapEvent mapEvent)
    {
        MobileParty = mobileParty;
        MapEvent = mapEvent;
    }
}
