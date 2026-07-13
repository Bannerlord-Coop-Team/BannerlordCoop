using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Data;

/// <summary>
/// Creates complete movement and AI behavior snapshots for mobile parties.
/// </summary>
public interface IMobilePartyBehaviorSnapshot
{
    bool TryCreateCurrent(
        MobileParty party,
        out PartyBehaviorUpdateData data);

    bool TryCreate(
        MobileParty party,
        AiBehavior shortTermBehavior,
        IInteractablePoint interactablePoint,
        CampaignVec2 behaviorTarget,
        CampaignVec2 moveTargetPoint,
        out PartyBehaviorUpdateData data);
}

/// <summary>
/// Builds the complete movement and AI behavior snapshot replicated for one mobile party.
/// </summary>
public sealed class MobilePartyBehaviorSnapshot : IMobilePartyBehaviorSnapshot
{
    private readonly IObjectManager objectManager;

    public MobilePartyBehaviorSnapshot(IObjectManager objectManager)
    {
        if (objectManager == null)
            throw new ArgumentNullException(nameof(objectManager));

        this.objectManager = objectManager;
    }

    public bool TryCreateCurrent(
        MobileParty party,
        out PartyBehaviorUpdateData data)
    {
        data = default;

        return party?.Ai != null &&
            TryCreate(
                party,
                party.ShortTermBehavior,
                party.Ai.AiBehaviorInteractable,
                party.Ai.BehaviorTarget,
                party.MoveTargetPoint,
                out data);
    }

    public bool TryCreate(
        MobileParty party,
        AiBehavior shortTermBehavior,
        IInteractablePoint interactablePoint,
        CampaignVec2 behaviorTarget,
        CampaignVec2 moveTargetPoint,
        out PartyBehaviorUpdateData data)
    {
        data = default;

        if (party == null)
            return false;

        if (!TryGetCompactId(party, out string partyId))
            return false;

        if (!TryGetInteractableReference(
                interactablePoint,
                out string interactablePointId,
                out BehaviorInteractableKind interactableKind,
                out bool hasTarget))
            return false;

        if (!TryGetCompactId(party.TargetParty, out string targetPartyId) ||
            !TryGetCompactId(party.TargetSettlement, out string targetSettlementId) ||
            !TryGetCompactId(party.MoveTargetParty, out string moveTargetPartyId))
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

    private bool TryGetInteractableReference(
        IInteractablePoint interactablePoint,
        out string interactablePointId,
        out BehaviorInteractableKind interactableKind,
        out bool hasTarget)
    {
        interactablePointId = null;
        interactableKind = BehaviorInteractableKind.PartyBase;
        hasTarget = false;

        switch (interactablePoint)
        {
            case PartyBase partyBase:
                hasTarget = true;
                return TryGetCompactId(partyBase, out interactablePointId) &&
                    !string.IsNullOrEmpty(interactablePointId);
            case AnchorPoint anchorPoint when anchorPoint.Owner != null:
                interactableKind = BehaviorInteractableKind.AnchorPoint;
                hasTarget = true;
                return TryGetCompactId(anchorPoint.Owner, out interactablePointId) &&
                    !string.IsNullOrEmpty(interactablePointId);
            case null:
                return true;
            default:
                // A successful result promises a complete snapshot. Refuse interactables that the
                // wire contract cannot represent instead of replaying a different null target.
                return false;
        }
    }

    private bool TryGetCompactId<T>(T instance, out string id)
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
