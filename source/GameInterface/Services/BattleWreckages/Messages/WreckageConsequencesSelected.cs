using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct WreckageConsequencesSelected : IEvent
{
    public readonly Hero MainHero;

    public WreckageConsequencesSelected(Hero mainHero)
    {
        MainHero = mainHero;
    }
}
