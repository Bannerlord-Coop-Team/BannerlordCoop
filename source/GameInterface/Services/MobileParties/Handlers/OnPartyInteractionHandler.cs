using Common.Messaging;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PlayerPartyInteractionHandler : IHandler
{
    private readonly IObjectManager objectManager;

    public PlayerPartyInteractionHandler(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Dispose()
    {
    }

    public bool TryHandleReciprocalPlayerInteraction(PartyBase targetParty, PartyBase engagingParty)
    {
        if (ShouldInitiateReciprocalPlayerInteraction(engagingParty, targetParty))
            EncounterManager.StartPartyEncounter(engagingParty, targetParty);

        return true;
    }

    internal bool ShouldInitiateReciprocalPlayerInteraction(PartyBase engagingParty, PartyBase targetParty)
    {
        if (!objectManager.TryGetId(engagingParty, out var engagingPartyId)) return false;
        if (!objectManager.TryGetId(targetParty, out var targetPartyId)) return false;

        return string.CompareOrdinal(engagingPartyId, targetPartyId) <= 0;
    }
}