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
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

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
        messageBroker.Subscribe<UpdatePartyBehavior>(Handle_UpdatePartyBehavior);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBehaviorChangeAttempted>(Handle_PartyBehaviorChanged);
        messageBroker.Unsubscribe<UpdatePartyBehavior>(Handle_UpdatePartyBehavior);
    }

    public void Handle_PartyBehaviorChanged(MessagePayload<PartyBehaviorChangeAttempted> obj)
    {
        var party = obj.What.Party;

        if (ModInformation.IsClient && !party.IsControlledByThisInstance())
            return;

        if (!mobilePartyBehaviorSnapshot.TryCreate(
                party,
                out PartyBehaviorUpdateData data))
            return;

        data.ForcePosition = obj.What.ForcePosition;
        data.IsCurrentlyAtSea = obj.What.IsCurrentlyAtSea;
        data.ResetMovementToHold = obj.What.ResetMovementToHold;

        if (ModInformation.IsClient)
        {
            data.OriginControllerId = controllerIdProvider.ControllerId;
            latestPredictions[data.MobilePartyId] = data;
            messageBroker.Publish(this, new ControlledPartyBehaviorUpdated(data));
            return;
        }

        messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
    }

    public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        GameThread.RunSafe(() =>
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

            List<MobileParty> attachedParties = null;
            if (ModInformation.IsServer && data.ForcePosition)
            {
                attachedParties = ApplyServerForcedPosition(party, data.PartyPosition, data.IsCurrentlyAtSea);
                if (data.ResetMovementToHold)
                {
                    party.SetMoveModeHold();
                    party.ResetNavigationToHold();
                }
            }

            IInteractablePoint interactablePoint = null;
            // AllowedThread keeps outbound movement patches quiet while the complete snapshot is replayed.
            using (new AllowedThread())
            {
                if ((!ModInformation.IsServer || !data.ResetMovementToHold) &&
                    !mobilePartyBehaviorSnapshot.TryApply(
                        party,
                        data,
                        out interactablePoint))
                    return;

                if (ModInformation.IsClient && data.ForcePosition)
                    ApplyForcedPosition(party, data.PartyPosition, data.IsCurrentlyAtSea);

                if (ModInformation.IsClient && data.ResetMovementToHold)
                {
                    party.SetMoveModeHold();
                    party.ResetNavigationToHold();
                }

                if (MobilePartyAiConfig.DEBUG)
                {
                    Logger.Debug(
                        "Setting AI behavior. PartyId: {PartyId}, Behavior: {Behavior}, TargetParty: {TargetParty}, BestTargetPoint: {BestTargetPoint}",
                        data.MobilePartyId,
                        data.NewAiBehavior,
                        interactablePoint,
                        data.BestTargetPoint);
                }

                if (ModInformation.IsClient)
                {
                    // Moving parties already simulate the replicated target, so an in-flight snapshot is stale.
                    if (!data.ForcePosition && ShouldApplyAuthoritativePosition(
                            isSelfEcho,
                            data.ForcePosition,
                            party.PartyMoveMode == MoveModeType.Hold,
                            party.Position,
                            data.PartyPosition))
                        party.Position = data.PartyPosition;
                }
            }

            if (ModInformation.IsServer)
                PublishAuthoritativeBehavior(party, data);

            if (attachedParties != null)
            {
                foreach (var attachedParty in attachedParties)
                    PublishForcedPosition(attachedParty);
            }
        });
    }

    private static void ApplyForcedPosition(MobileParty party, CampaignVec2 position, bool isCurrentlyAtSea)
    {
        party.Position = position;

        if (party.IsCurrentlyAtSea != isCurrentlyAtSea)
            party.ChangeIsCurrentlyAtSeaCheat();
    }

    private static List<MobileParty> ApplyServerForcedPosition(
        MobileParty party,
        CampaignVec2 position,
        bool isCurrentlyAtSea)
    {
        ApplyForcedPosition(party, position, isCurrentlyAtSea);

        if (party.Army == null)
            return null;

        List<MobileParty> attachedParties = null;
        foreach (var attachedParty in party.Army.LeaderParty.AttachedParties)
        {
            if (attachedParty == party)
                continue;

            attachedParty.Position = position;
            attachedParties ??= new List<MobileParty>();
            attachedParties.Add(attachedParty);
        }

        return attachedParties;
    }

    private void PublishForcedPosition(MobileParty party)
    {
        if (!mobilePartyBehaviorSnapshot.TryCreate(
                party,
                out PartyBehaviorUpdateData data))
            return;

        data.ForcePosition = true;
        messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
    }

    private void PublishAuthoritativeBehavior(MobileParty party, PartyBehaviorUpdateData request)
    {
        if (!mobilePartyBehaviorSnapshot.TryCreate(
                party,
                out PartyBehaviorUpdateData authoritativeData))
            return;

        authoritativeData.OriginControllerId = request.OriginControllerId;
        authoritativeData.ForcePosition = request.ForcePosition;
        authoritativeData.ResetMovementToHold = request.ResetMovementToHold;
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
