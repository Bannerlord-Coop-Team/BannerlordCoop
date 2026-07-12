using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Data;

/// <summary>
/// Builds the complete movement and AI behavior snapshot replicated for one mobile party.
/// </summary>
public static class MobilePartyBehaviorSnapshot
{
    public static bool TryCreateCurrent(
        IObjectManager objectManager,
        MobileParty party,
        out PartyBehaviorUpdateData data)
    {
        data = default;

        return party?.Ai != null &&
            TryCreate(
                objectManager,
                party,
                party.ShortTermBehavior,
                party.Ai.AiBehaviorInteractable,
                party.Ai.BehaviorTarget,
                party.MoveTargetPoint,
                out data);
    }

    public static bool TryCreate(
        IObjectManager objectManager,
        MobileParty party,
        AiBehavior shortTermBehavior,
        IInteractablePoint interactablePoint,
        CampaignVec2 behaviorTarget,
        CampaignVec2 moveTargetPoint,
        out PartyBehaviorUpdateData data)
    {
        data = default;

        if (objectManager == null || party == null)
            return false;

        if (!TryGetCompactId(objectManager, party, out string partyId))
            return false;

        string interactablePointId = null;
        var interactableKind = BehaviorInteractableKind.PartyBase;
        bool hasTarget = false;
        switch (interactablePoint)
        {
            case PartyBase partyBase:
                if (!TryGetCompactId(objectManager, partyBase, out interactablePointId))
                    return false;
                hasTarget = true;
                break;
            case AnchorPoint anchorPoint when anchorPoint.Owner != null:
                if (!TryGetCompactId(objectManager, anchorPoint.Owner, out interactablePointId))
                    return false;
                interactableKind = BehaviorInteractableKind.AnchorPoint;
                hasTarget = true;
                break;
            case null:
                break;
            default:
                // Preserve the rest of the movement snapshot when an unknown vanilla interactable
                // cannot be represented yet; replay falls back to no interactable.
                break;
        }

        if (hasTarget && string.IsNullOrEmpty(interactablePointId))
            return false;

        if (!TryGetCompactId(objectManager, party.TargetParty, out string targetPartyId) ||
            !TryGetCompactId(objectManager, party.TargetSettlement, out string targetSettlementId) ||
            !TryGetCompactId(objectManager, party.MoveTargetParty, out string moveTargetPartyId))
            return false;

        data = new PartyBehaviorUpdateData(
            partyId,
            shortTermBehavior,
            interactablePointId,
            behaviorTarget,
            hasTarget,
            party.Position,
            party.DefaultBehavior,
            party.TargetPosition,
            party.DesiredAiNavigationType)
        {
            TargetPartyId = targetPartyId,
            TargetSettlementId = targetSettlementId,
            MoveTargetPoint = moveTargetPoint,
            IsTargetingPort = party.IsTargetingPort,
            PartyMoveMode = party.PartyMoveMode,
            MoveTargetPartyId = moveTargetPartyId,
            NextTargetPosition = party.NextTargetPosition,
            InteractableKind = interactableKind,
        };

        return true;
    }

    private static bool TryGetCompactId<T>(IObjectManager objectManager, T instance, out string id)
        where T : class
    {
        id = null;
        if (instance == null)
            return true;

        if (!objectManager.TryGetId(instance, out id))
            return false;

        id = global::GameInterface.Services.ObjectManager.ObjectManager.Compact(id, typeof(T));
        return true;
    }
}
