using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Data;

public interface IPartyBehaviorWireMapper
{
    bool TryToNetwork(PartyBehaviorUpdateData source, out NetworkPartyBehaviorUpdateData destination);
    bool TryFromNetwork(NetworkPartyBehaviorUpdateData source, out PartyBehaviorUpdateData destination);
}

internal sealed class PartyBehaviorWireMapper : IPartyBehaviorWireMapper
{
    private readonly IObjectManager objectManager;

    public PartyBehaviorWireMapper(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryToNetwork(PartyBehaviorUpdateData source, out NetworkPartyBehaviorUpdateData destination)
    {
        destination = default;
        if (!TryGetHandle<MobileParty>(source.MobilePartyId, out var mobilePartyId) ||
            !TryGetHandle<MobileParty>(source.TargetPartyId, out var targetPartyId) ||
            !TryGetHandle<Settlement>(source.TargetSettlementId, out var targetSettlementId) ||
            !TryGetHandle<MobileParty>(source.MoveTargetPartyId, out var moveTargetPartyId))
            return false;

        uint interactablePointId;
        if (source.IsInteractableAnchor)
        {
            if (!TryGetHandle<MobileParty>(source.InteractablePointId, out interactablePointId)) return false;
        }
        else if (!TryGetHandle<PartyBase>(source.InteractablePointId, out interactablePointId))
        {
            return false;
        }

        destination = new NetworkPartyBehaviorUpdateData
        {
            MobilePartyId = mobilePartyId,
            NewAiBehavior = source.NewAiBehavior,
            InteractablePointId = interactablePointId,
            BestTargetPoint = source.BestTargetPoint,
            PartyPosition = source.PartyPosition,
            DefaultBehavior = source.DefaultBehavior,
            TargetPosition = source.TargetPosition,
            DesiredAiNavigationType = source.DesiredAiNavigationType,
            OriginControllerId = source.OriginControllerId,
            ForcePosition = source.ForcePosition,
            TargetPartyId = targetPartyId,
            TargetSettlementId = targetSettlementId,
            MoveTargetPoint = source.MoveTargetPoint,
            IsTargetingPort = source.IsTargetingPort,
            PartyMoveMode = source.PartyMoveMode,
            MoveTargetPartyId = moveTargetPartyId,
            IsInteractableAnchor = source.IsInteractableAnchor,
            IsCurrentlyAtSea = source.IsCurrentlyAtSea,
            ResetMovementToHold = source.ResetMovementToHold,
        };
        return true;
    }

    public bool TryFromNetwork(NetworkPartyBehaviorUpdateData source, out PartyBehaviorUpdateData destination)
    {
        destination = default;
        if (!TryGetId<MobileParty>(source.MobilePartyId, out var mobilePartyId) ||
            !TryGetId<MobileParty>(source.TargetPartyId, out var targetPartyId) ||
            !TryGetId<Settlement>(source.TargetSettlementId, out var targetSettlementId) ||
            !TryGetId<MobileParty>(source.MoveTargetPartyId, out var moveTargetPartyId))
            return false;

        string interactablePointId;
        if (source.IsInteractableAnchor)
        {
            if (!TryGetId<MobileParty>(source.InteractablePointId, out interactablePointId)) return false;
        }
        else if (!TryGetId<PartyBase>(source.InteractablePointId, out interactablePointId))
        {
            return false;
        }

        destination = new PartyBehaviorUpdateData(
            mobilePartyId,
            source.NewAiBehavior,
            interactablePointId,
            source.BestTargetPoint,
            source.PartyPosition,
            source.DefaultBehavior,
            source.TargetPosition,
            source.DesiredAiNavigationType)
        {
            OriginControllerId = source.OriginControllerId,
            ForcePosition = source.ForcePosition,
            TargetPartyId = targetPartyId,
            TargetSettlementId = targetSettlementId,
            MoveTargetPoint = source.MoveTargetPoint,
            IsTargetingPort = source.IsTargetingPort,
            PartyMoveMode = source.PartyMoveMode,
            MoveTargetPartyId = moveTargetPartyId,
            IsInteractableAnchor = source.IsInteractableAnchor,
            IsCurrentlyAtSea = source.IsCurrentlyAtSea,
            ResetMovementToHold = source.ResetMovementToHold,
        };
        return true;
    }

    private bool TryGetHandle<T>(string id, out uint handle) where T : class
    {
        handle = 0;
        if (id == null) return true;
        return objectManager.TryGetObjectWithLogging(id, out T value) &&
            objectManager.TryGetHandleWithLogging(value, out handle);
    }

    private bool TryGetId<T>(uint handle, out string id) where T : class
    {
        id = null;
        if (handle == 0) return true;
        return objectManager.TryGetObjectWithLogging(handle, out T value) &&
            objectManager.TryGetIdWithLogging(value, out id);
    }
}
