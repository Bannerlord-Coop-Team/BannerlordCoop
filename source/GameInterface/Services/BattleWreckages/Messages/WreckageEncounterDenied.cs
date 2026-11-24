using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct WreckageEncounterDenied : IEvent
{
    public readonly Hero MainHero;

    public WreckageEncounterDenied(Hero mainHero)
    {
        MainHero = mainHero;
    }
}
