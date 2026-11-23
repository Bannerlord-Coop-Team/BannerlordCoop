using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct WreckageInvestigationResultsApplied : IEvent
{
    public readonly Hero MainHero;

    public WreckageInvestigationResultsApplied(Hero mainHero)
    {
        MainHero = mainHero;
    }
}
