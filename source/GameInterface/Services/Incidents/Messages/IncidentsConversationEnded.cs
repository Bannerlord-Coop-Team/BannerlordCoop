using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Incidents.Messages;

public readonly struct IncidentsConversationEnded : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;

    public IncidentsConversationEnded(Hero mainHero, MobileParty mainParty)
    {
        MainHero = mainHero;
        MainParty = mainParty;
    }
}
