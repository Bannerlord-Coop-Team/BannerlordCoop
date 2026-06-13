using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When a hero is attached to hero cache
/// </summary>
public readonly struct SettlementChangedAddHeroWithoutParty : IEvent
{
    public readonly Settlement Settlement;
    public readonly Hero Hero;

    public SettlementChangedAddHeroWithoutParty(Settlement settlement, Hero hero)
    {
        Settlement = settlement;
        Hero = hero;
    }
}
