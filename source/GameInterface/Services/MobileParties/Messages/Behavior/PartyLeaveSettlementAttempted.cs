using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a party attempts to leave a settlement
/// </summary>
public readonly struct PartyLeaveSettlementAttempted : IEvent
{
    public readonly MobileParty MobileParty;

    public PartyLeaveSettlementAttempted(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
