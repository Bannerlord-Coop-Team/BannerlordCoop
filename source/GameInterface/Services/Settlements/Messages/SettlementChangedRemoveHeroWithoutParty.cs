using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Settlement HeroWithoutParty was removed notify server.
/// </summary>
[BatchLogMessage]
public readonly struct SettlementChangedRemoveHeroWithoutParty : IEvent
{
    public readonly Settlement Settlement;
    public readonly Hero Hero;

    public SettlementChangedRemoveHeroWithoutParty(Settlement settlement, Hero hero)
    {
        Settlement = settlement;
        Hero = hero;
    }
}
