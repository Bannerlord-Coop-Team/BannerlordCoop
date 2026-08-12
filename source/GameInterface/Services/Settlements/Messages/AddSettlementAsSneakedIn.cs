using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

public readonly struct AddSettlementAsSneakedIn : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement CurrentSettlement;

    public AddSettlementAsSneakedIn(
        Hero mainHero,
        Settlement currentSettlement)
    {
        MainHero = mainHero;
        CurrentSettlement = currentSettlement;
    }
}
