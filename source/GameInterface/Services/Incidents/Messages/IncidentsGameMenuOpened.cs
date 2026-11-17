using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Incidents.Messages;

public readonly struct IncidentsGameMenuOpened : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly string MenuContextId;

    public IncidentsGameMenuOpened(
        Hero mainHero,
        MobileParty mainParty,
        string menuContextId)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        MenuContextId = menuContextId;
    }
}
