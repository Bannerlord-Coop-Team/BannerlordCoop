using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

internal class ReciprocalPlayerPartyInteractionAttempted : IEvent
{
    public readonly PartyBase TargetParty;
    public readonly PartyBase EngagingParty;

    public ReciprocalPlayerPartyInteractionAttempted(PartyBase targetParty, PartyBase engagingParty)
    {
        TargetParty = targetParty;
        EngagingParty = engagingParty;
    }
}
