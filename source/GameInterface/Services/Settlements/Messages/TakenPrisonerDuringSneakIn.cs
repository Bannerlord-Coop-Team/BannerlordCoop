using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

public readonly struct TakenPrisonerDuringSneakIn : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement CurrentSettlement;

    public TakenPrisonerDuringSneakIn(
        Hero mainHero,
        Settlement currentSettlement)
    {
        MainHero = mainHero;
        CurrentSettlement = currentSettlement;
    }
}
