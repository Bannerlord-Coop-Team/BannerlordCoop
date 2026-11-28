using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct RequestSelectWreckageConsequences : IEvent
{
    public readonly Hero MainHero;

    public RequestSelectWreckageConsequences(Hero mainHero)
    {
        MainHero = mainHero;
    }
}
