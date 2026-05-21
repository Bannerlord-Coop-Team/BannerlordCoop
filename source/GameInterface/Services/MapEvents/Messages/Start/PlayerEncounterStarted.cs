using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Start;

public readonly struct PlayerEncounterStarted : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly MapEvent MapEvent;

    public PlayerEncounterStarted(MobileParty mobileParty, MapEvent mapEvent)
    {
        MobileParty = mobileParty;
        MapEvent = mapEvent;
    }
}
