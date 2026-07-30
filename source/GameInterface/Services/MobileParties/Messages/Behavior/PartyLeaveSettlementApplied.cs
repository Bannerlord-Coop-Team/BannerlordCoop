using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered after a party successfully leaves a settlement on the server.
/// </summary>
public readonly struct PartyLeaveSettlementApplied : IEvent
{
    public readonly MobileParty MobileParty;

    public PartyLeaveSettlementApplied(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
