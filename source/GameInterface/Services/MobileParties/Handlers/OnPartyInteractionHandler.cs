using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PlayerPartyInteractionHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public PlayerPartyInteractionHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ReciprocalPlayerPartyInteractionAttempted>(Handle_ReciprocalPlayerPartyInteractionAttempted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ReciprocalPlayerPartyInteractionAttempted>(Handle_ReciprocalPlayerPartyInteractionAttempted);
    }

    private void Handle_ReciprocalPlayerPartyInteractionAttempted(MessagePayload<ReciprocalPlayerPartyInteractionAttempted> payload)
    {
        var message = payload.What;

        if (ShouldInitiateReciprocalPlayerInteraction(message.EngagingParty, message.TargetParty))
            EncounterManager.StartPartyEncounter(message.EngagingParty, message.TargetParty);
    }

    internal bool ShouldInitiateReciprocalPlayerInteraction(PartyBase engagingParty, PartyBase targetParty)
    {
        if (!objectManager.TryGetId(engagingParty, out var engagingPartyId)) return false;
        if (!objectManager.TryGetId(targetParty, out var targetPartyId)) return false;

        return string.CompareOrdinal(engagingPartyId, targetPartyId) <= 0;
    }
}