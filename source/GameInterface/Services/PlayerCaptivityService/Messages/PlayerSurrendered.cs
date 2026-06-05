using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

public readonly struct PlayerSurrendered : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly MobileParty PlayerParty;

    public PlayerSurrendered(MapEvent mapEvent, MobileParty mobileParty)
    {
        MapEvent = mapEvent;
        PlayerParty = mobileParty;
    }
}