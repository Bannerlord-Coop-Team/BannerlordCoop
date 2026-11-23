using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct RequestAcceptRecoverTroops : IEvent
{
    public readonly Hero MainHero;

    public RequestAcceptRecoverTroops(Hero mainHero)
    {
        MainHero = mainHero;
    }
}
