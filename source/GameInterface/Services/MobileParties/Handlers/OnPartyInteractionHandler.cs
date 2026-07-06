using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class OnPartyInteractionHandler : IHandler
{
    private readonly IObjectManager objectManager;

    public OnPartyInteractionHandler(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Dispose()
    {
    }

    public bool TryHandleReciprocalPlayerInteraction(MobileParty targetParty, MobileParty engagingParty)
    {
        if (!CanHandleReciprocalPlayerInteraction(targetParty, engagingParty)) return false;
        if (!IsReciprocalPlayerInteractionReady(targetParty, engagingParty)) return false;
        if (!TryGetPartyBases(targetParty, engagingParty, out var targetPartyBase, out var engagingPartyBase))
            return false;

        if (ShouldInitiateReciprocalPlayerInteraction(engagingPartyBase, targetPartyBase))
            EncounterManager.StartPartyEncounter(engagingPartyBase, targetPartyBase);

        return true;
    }

    internal static bool CanHandleReciprocalPlayerInteraction(MobileParty targetParty, MobileParty engagingParty)
    {
        if (ModInformation.IsServer) return false;
        if (targetParty == null || engagingParty == null) return false;
        if (targetParty == engagingParty) return false;
        if (!engagingParty.IsMainParty) return false;
        if (!engagingParty.IsControlledByThisInstance()) return false;
        if (!targetParty.IsPlayerParty()) return false;

        return true;
    }

    internal static bool IsReciprocalPlayerInteractionReady(MobileParty targetParty, MobileParty engagingParty)
    {
        if (targetParty.CurrentSettlement != null) return false;
        if (targetParty.MapEvent != null || engagingParty.MapEvent != null) return false;
        if (!targetParty.IsEngaging) return false;
        if (targetParty.ShortTermTargetParty != engagingParty) return false;

        return true;
    }

    internal static bool TryGetPartyBases(
        MobileParty targetParty,
        MobileParty engagingParty,
        out PartyBase targetPartyBase,
        out PartyBase engagingPartyBase)
    {
        targetPartyBase = targetParty.Party;
        engagingPartyBase = engagingParty.Party;

        return targetPartyBase != null && engagingPartyBase != null;
    }

    internal bool ShouldInitiateReciprocalPlayerInteraction(PartyBase engagingParty, PartyBase targetParty)
    {
        if (!objectManager.TryGetId(engagingParty, out var engagingPartyId)) return false;
        if (!objectManager.TryGetId(targetParty, out var targetPartyId)) return false;

        return string.CompareOrdinal(engagingPartyId, targetPartyId) <= 0;
    }
}