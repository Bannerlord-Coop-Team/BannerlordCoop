using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Incidents.Messages;

public readonly struct IncidentOptionInvoked : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly Incident Incident;
    public readonly string OptionTextId;

    public IncidentOptionInvoked(
        Hero mainHero,
        MobileParty mainParty,
        Incident incident,
        string optionTextId)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        Incident = incident;
        OptionTextId = optionTextId;
    }
}
