using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal readonly struct PlayerLeaveBattle : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly MobileParty MobileParty;

    public PlayerLeaveBattle(MapEvent mapEvent, MobileParty mobileParty)
    {
        MapEvent = mapEvent;
        MobileParty = mobileParty;
    }
}