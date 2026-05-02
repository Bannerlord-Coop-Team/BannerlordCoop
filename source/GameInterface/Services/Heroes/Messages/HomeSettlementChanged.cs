using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _homeSettlement.
/// </summary>
public readonly struct HomeSettlementChanged : IEvent
{
    public readonly Hero Hero;
    public readonly Settlement Settlement;

    public HomeSettlementChanged(Settlement settlement, Hero hero)
    {
        Settlement = settlement;
        Hero = hero;
    }
}