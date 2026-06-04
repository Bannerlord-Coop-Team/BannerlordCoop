using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Leave;

public readonly struct PlayerSurrendered : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly MobileParty MobileParty;

    public PlayerSurrendered(MapEvent mapEvent, MobileParty mobileParty)
    {
        MapEvent = mapEvent;
        MobileParty = mobileParty;
    }
}