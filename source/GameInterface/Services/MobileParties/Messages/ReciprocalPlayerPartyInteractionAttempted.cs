using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

internal class ReciprocalPlayerPartyInteractionAttempted : IEvent
{
    public readonly PartyBase TargetParty;
    public readonly PartyBase EngagingParty;

    public bool Handled { get; private set; }

    public ReciprocalPlayerPartyInteractionAttempted(PartyBase targetParty, PartyBase engagingParty)
    {
        TargetParty = targetParty;
        EngagingParty = engagingParty;
    }

    public void SetHandled()
    {
        Handled = true;
    }
}
