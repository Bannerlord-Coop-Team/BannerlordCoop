using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Data;

public interface IMobilePartyBehaviorSnapshot
{
    bool TryCreate(MobileParty party, out PartyBehaviorUpdateData data);
    bool TryApply(MobileParty party, PartyBehaviorUpdateData data, out IInteractablePoint interactable);
}

public sealed class MobilePartyBehaviorSnapshot : IMobilePartyBehaviorSnapshot
{
    private readonly IObjectManager objectManager;

    public MobilePartyBehaviorSnapshot(IObjectManager objectManager) => this.objectManager = objectManager;

    public bool TryCreate(
        MobileParty party,
        out PartyBehaviorUpdateData data)
    {
        data = default;
        if (party?.Ai == null)
            return false;
        if (!TryGetCompactId(party, out string partyId) ||
            !TryGetInteractableReference(
                party.Ai.AiBehaviorInteractable,
                out string interactablePointId,
                out bool isInteractableAnchor) ||
            !TryGetCompactId(party.TargetParty, out string targetPartyId) ||
            !TryGetCompactId(party.TargetSettlement, out string targetSettlementId) ||
            !TryGetCompactId(party.MoveTargetParty, out string moveTargetPartyId))
            return false;
        data = new PartyBehaviorUpdateData(
            partyId,
            party.ShortTermBehavior,
            interactablePointId,
            party.Ai.BehaviorTarget,
            party.Position,
            party.DefaultBehavior,
            party.TargetPosition,
            party.DesiredAiNavigationType)
        {
            TargetPartyId = targetPartyId,
            TargetSettlementId = targetSettlementId,
            MoveTargetPoint = party.MoveTargetPoint,
            IsTargetingPort = party.IsTargetingPort,
            PartyMoveMode = party.PartyMoveMode,
            MoveTargetPartyId = moveTargetPartyId,
            IsInteractableAnchor = isInteractableAnchor,
        };
        return true;
    }

    public bool TryApply(MobileParty party, PartyBehaviorUpdateData data, out IInteractablePoint interactable)
    {
        interactable = null;
        if (party?.Ai == null ||
            !TryResolveInteractable(data, out interactable) ||
            !TryResolve(data.TargetPartyId, out MobileParty targetParty) ||
            !TryResolve(data.TargetSettlementId, out Settlement targetSettlement) ||
            !TryResolve(data.MoveTargetPartyId, out MobileParty moveTargetParty))
            return false;
        // Install targets first because DefaultBehavior can immediately recalculate short-term state.
        party.SetTargetSettlement(targetSettlement, data.IsTargetingPort);
        party.TargetParty = targetParty;
        party.TargetPosition = data.TargetPosition;
        party.DefaultBehavior = data.DefaultBehavior;
        party.SetShortTermBehavior(data.NewAiBehavior, interactable);
        party.DesiredAiNavigationType = data.DesiredAiNavigationType;
        party.Ai.BehaviorTarget = data.BestTargetPoint;
        party.Ai.UpdateBehavior();
        // Use vanilla setters so private path state stays aligned with the replicated move mode.
        switch (data.PartyMoveMode)
        {
            case MoveModeType.Hold:
                party.SetNavigationModeHold();
                break;
            case MoveModeType.Point:
                party.SetNavigationModePoint(data.MoveTargetPoint);
                break;
            case MoveModeType.Party when moveTargetParty != null:
                party.SetNavigationModeParty(moveTargetParty);
                break;
            default:
                party.PartyMoveMode = data.PartyMoveMode;
                party.MoveTargetParty = moveTargetParty;
                break;
        }
        party.MoveTargetPoint = data.MoveTargetPoint;
        return true;
    }

    private bool TryResolveInteractable(PartyBehaviorUpdateData data, out IInteractablePoint interactable)
    {
        interactable = null;
        if (data.InteractablePointId == null)
            return true;
        if (data.IsInteractableAnchor)
            return TryResolve(data.InteractablePointId, out MobileParty owner) &&
                (interactable = owner.Anchor) != null;
        return TryResolve(data.InteractablePointId, out PartyBase partyBase) &&
            (interactable = partyBase) != null;
    }

    private bool TryResolve<T>(string id, out T value) where T : class
    {
        value = null;
        return id == null || objectManager.TryGetObjectWithLogging(id, out value);
    }

    private bool TryGetInteractableReference(IInteractablePoint interactable, out string id, out bool isAnchor)
    {
        isAnchor = interactable is AnchorPoint;
        if (interactable is PartyBase partyBase)
            return TryGetCompactId(partyBase, out id);
        if (interactable is AnchorPoint anchor && anchor.Owner != null)
            return TryGetCompactId(anchor.Owner, out id);
        id = null;
        return interactable == null;
    }

    private bool TryGetCompactId<T>(T instance, out string id)
        where T : class
    {
        if (instance != null && objectManager.TryGetId(instance, out id))
        {
            id = Compact(id, typeof(T));
            return true;
        }
        id = null;
        return instance == null;
    }
}
