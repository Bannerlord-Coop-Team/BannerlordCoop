using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct RequestStartWreckageEncounter : IEvent
{
    public readonly BattleWreckage BattleWreckage;
    public readonly Hero MainHero;

    public RequestStartWreckageEncounter(
        BattleWreckage battleWreckage,
        Hero mainHero)
    {
        BattleWreckage = battleWreckage;
        MainHero = mainHero;
    }
}
