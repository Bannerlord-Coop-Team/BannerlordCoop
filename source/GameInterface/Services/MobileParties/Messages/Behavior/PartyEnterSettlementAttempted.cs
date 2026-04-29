using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a party attempts to enter a settlement
/// </summary>
[BatchLogMessage]
public readonly struct PartyEnterSettlementAttempted : IEvent
{
    public readonly Settlement Settlement;
    public readonly MobileParty MobileParty;

    public PartyEnterSettlementAttempted(Settlement settlement, MobileParty mobileParty)
    {
        Settlement = settlement;
        MobileParty = mobileParty;
    }
}
