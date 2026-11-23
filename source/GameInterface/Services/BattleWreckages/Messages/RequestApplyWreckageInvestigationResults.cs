using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct RequestApplyWreckageInvestigationResults : IEvent
{
    public readonly Hero MainHero;

    public RequestApplyWreckageInvestigationResults(Hero mainHero)
    {
        MainHero = mainHero;
    }
}
