using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Incidents.Messages;

public readonly struct IncidentsGameMenuOptionSelected : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly string GameMenuId;
    public readonly string OptionId;

    public IncidentsGameMenuOptionSelected(
        Hero mainHero,
        MobileParty mainParty,
        string gameMenuId,
        string optionId)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        GameMenuId = gameMenuId;
        OptionId = optionId;
    }
}
