using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobilePartyAIs;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles synchronization of the <see cref="MobilePartyAi"/>'s behavior on the campaign map, which includes
/// target positions and target entities used for updating movement.
/// </summary>
/// <remarks>
/// Important note: <see cref="MobilePartyAi"/> is also present in player-controlled parties, where it is 
/// responsible for pathfinding and movement.
/// </remarks>
/// <seealso cref="AiBehavior"/>
internal class MobilePartyBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyBehaviorHandler>();

    private readonly Dictionary<string, PartyBehaviorUpdateData> latestPredictions = new Dictionary<string, PartyBehaviorUpdateData>();
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMobilePartyInterface mobilePartyInterface;
    private readonly IObjectManager objectManager;
    private readonly IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot;

    public MobilePartyBehaviorHandler(
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IMobilePartyInterface mobilePartyInterface,
        IObjectManager objectManager,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot)
    {
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        this.mobilePartyInterface = mobilePartyInterface;
        this.objectManager = objectManager;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;

        messageBroker.Subscribe<PartyBehaviorChangeAttempted>(Handle_PartyBehaviorChanged);
        messageBroker.Subscribe<MobilePartyMovementStateChanged>(Handle_MobilePartyMovementStateChanged);
        messageBroker.Subscribe<UpdatePartyBehavior>(Handle_UpdatePartyBehavior);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBehaviorChangeAttempted>(Handle_PartyBehaviorChanged);
        messageBroker.Unsubscribe<MobilePartyMovementStateChanged>(Handle_MobilePartyMovementStateChanged);
        messageBroker.Unsubscribe<UpdatePartyBehavior>(Handle_UpdatePartyBehavior);
    }

    public void Handle_PartyBehaviorChanged(MessagePayload<PartyBehaviorChangeAttempted> obj)
    {
        var party = obj.What.PartyAi._mobileParty;

        if (!party.IsControlledByThisInstance())
            return;

        if (!mobilePartyBehaviorSnapshot.TryCreateCurrent(
                party,
                out PartyBehaviorUpdateData data))
            return;

        if (ModInformation.IsClient)
        {
            data.OriginControllerId = controllerIdProvider.ControllerId;
            latestPredictions[data.MobilePartyId] = data;
            messageBroker.Publish(this, new ControlledPartyBehaviorUpdated(data));
            return;
        }

        messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
    }

    private void Handle_MobilePartyMovementStateChanged(MessagePayload<MobilePartyMovementStateChanged> obj)
    {
        var party = obj.What.Party;
        if (!mobilePartyBehaviorSnapshot.TryCreateCurrent(
                party,
                out PartyBehaviorUpdateData data))
            return;

        messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
    }

    public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        GameThread.Run(() => ApplyBehaviorUpdateSafely(data));
    }

    private void ApplyBehaviorUpdateSafely(PartyBehaviorUpdateData data)
    {
        try
        {
            ApplyBehaviorUpdate(data);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to apply {Message}", nameof(UpdatePartyBehavior));
        }
    }

    private void ApplyBehaviorUpdate(PartyBehaviorUpdateData data)
    {
        if (!objectManager.TryGetObjectWithLogging(data.MobilePartyId, out MobileParty party))
            return;

        bool isSelfEcho = ModInformation.IsClient &&
            party.IsControlledByThisInstance() &&
            !string.IsNullOrEmpty(data.OriginControllerId) &&
            string.Equals(data.OriginControllerId, controllerIdProvider.ControllerId, StringComparison.Ordinal);

        // Reapply the newest local command if an authoritative update arrived before its echo.
        if (!TrySelectBehaviorUpdate(isSelfEcho, latestPredictions, ref data))
            return;

        if (!TryResolveSnapshotReferences(
                data,
                out IInteractablePoint interactablePoint,
                out MobileParty targetParty,
                out Settlement targetSettlement,
                out MobileParty moveTargetParty))
            return;

        // AllowedThread keeps outbound movement patches quiet while the complete snapshot is replayed.
        using (new AllowedThread())
        {
            if (!PartyBehaviorPatch.ApplyBehaviorSnapshot(
                    party.Ai,
                    data,
                    interactablePoint,
                    targetParty,
                    targetSettlement,
                    moveTargetParty))
                return;

            LogBehaviorUpdate(data, interactablePoint);
            FinishBehaviorUpdate(party, data, isSelfEcho);
        }
    }

    private bool TryResolveSnapshotReferences(
        PartyBehaviorUpdateData data,
        out IInteractablePoint interactablePoint,
        out MobileParty targetParty,
        out Settlement targetSettlement,
        out MobileParty moveTargetParty)
    {
        targetParty = null;
        targetSettlement = null;
        moveTargetParty = null;

        return TryResolveInteractablePoint(data, out interactablePoint) &&
            (data.TargetPartyId == null ||
                objectManager.TryGetObjectWithLogging(data.TargetPartyId, out targetParty)) &&
            (data.TargetSettlementId == null ||
                objectManager.TryGetObjectWithLogging(data.TargetSettlementId, out targetSettlement)) &&
            (data.MoveTargetPartyId == null ||
                objectManager.TryGetObjectWithLogging(data.MoveTargetPartyId, out moveTargetParty));
    }

    private static void LogBehaviorUpdate(
        PartyBehaviorUpdateData data,
        IInteractablePoint interactablePoint)
    {
        if (!MobilePartyAiConfig.DEBUG)
            return;

        Logger.Debug(
            "Setting AI behavior. PartyId: {PartyId}, Behavior: {Behavior}, TargetParty: {TargetParty}, BestTargetPoint: {BestTargetPoint}",
            data.MobilePartyId,
            data.NewAiBehavior,
            interactablePoint,
            data.BestTargetPoint);
    }

    private void FinishBehaviorUpdate(
        MobileParty party,
        PartyBehaviorUpdateData data,
        bool isSelfEcho)
    {
        if (ModInformation.IsServer)
        {
            PublishAuthoritativeBehavior(party, data);
            return;
        }

        // Moving parties already simulate the replicated target, so an in-flight snapshot is stale.
        if (ShouldApplyAuthoritativePosition(
                isSelfEcho,
                data.ForcePosition,
                party.PartyMoveMode == MoveModeType.Hold,
                party.Position,
                data.PartyPosition))
            party.Position = data.PartyPosition;
    }

    private bool TryResolveInteractablePoint(
        PartyBehaviorUpdateData data,
        out IInteractablePoint interactablePoint)
    {
        interactablePoint = null;
        if (!data.HasTarget)
            return true;

        if (data.InteractableKind == BehaviorInteractableKind.AnchorPoint)
        {
            if (!objectManager.TryGetObjectWithLogging(data.InteractablePointId, out MobileParty owner))
                return false;

            interactablePoint = owner.Anchor;
            return interactablePoint != null;
        }

        if (!objectManager.TryGetObjectWithLogging(data.InteractablePointId, out PartyBase partyBase))
            return false;

        interactablePoint = partyBase;
        return true;
    }

    private void PublishAuthoritativeBehavior(MobileParty party, PartyBehaviorUpdateData request)
    {
        if (!mobilePartyBehaviorSnapshot.TryCreateCurrent(
                party,
                out PartyBehaviorUpdateData authoritativeData))
            return;

        authoritativeData.OriginControllerId = request.OriginControllerId;
        authoritativeData.ForcePosition = request.ForcePosition;
        messageBroker.Publish(this, new PartyBehaviorUpdated(ref authoritativeData));
    }

    internal static bool TrySelectBehaviorUpdate(
        bool isSelfEcho,
        IReadOnlyDictionary<string, PartyBehaviorUpdateData> latestPredictions,
        ref PartyBehaviorUpdateData data)
    {
        if (!isSelfEcho)
            return true;

        var partyId = Compact(data.MobilePartyId, typeof(MobileParty));
        return latestPredictions.TryGetValue(partyId, out data);
    }

    internal static bool ShouldApplyAuthoritativePosition(
        bool isSelfEcho,
        bool forcePosition,
        bool isHolding,
        CampaignVec2 currentPosition,
        CampaignVec2 authoritativePosition)
    {
        return !isSelfEcho &&
            (forcePosition || isHolding || currentPosition.IsOnLand != authoritativePosition.IsOnLand);
    }
}
