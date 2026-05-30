using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

internal readonly struct PartyDisbanded : IEvent
{
    public readonly MobileParty DisbandedParty;
    public readonly Settlement RelatedSettlement;

    public PartyDisbanded(MobileParty disbandedParty, Settlement relatedSettlement)
    {
        DisbandedParty = disbandedParty;
        RelatedSettlement = relatedSettlement;
    }
}
