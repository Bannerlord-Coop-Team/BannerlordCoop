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

    public MobilePartyBehaviorHandler(
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IMobilePartyInterface mobilePartyInterface,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        this.mobilePartyInterface = mobilePartyInterface;
        this.objectManager = objectManager;

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
        var partyAi = obj.What.PartyAi;
        var party = obj.What.PartyAi._mobileParty;
        var interactablePoint = obj.What.InteractablePoint;

        var controllerId = controllerIdProvider.ControllerId;

        if (!objectManager.TryGetId(partyAi._mobileParty, out var partyId))
            return;

        if (!partyAi._mobileParty.IsControlledByThisInstance())
            return;

        partyId = Compact(partyId, typeof(MobileParty));

        string interactablePointId = null;
        if (interactablePoint is PartyBase partyBase &&
            !objectManager.TryGetId(partyBase, out interactablePointId))
            return;

        interactablePointId = Compact(interactablePointId, typeof(PartyBase));

        PartyBehaviorUpdateData data = new PartyBehaviorUpdateData(
            partyId,
            obj.What.NewAiBehavior,
            interactablePointId,
            obj.What.BestTargetPoint,
            interactablePoint is not null,
            party.Position,
            party.DefaultBehavior,
            party.TargetPosition,
            party.DesiredAiNavigationType
        );

        if (ModInformation.IsClient)
        {
            data.OriginControllerId = controllerId;
            latestPredictions[partyId] = data;
        }

        messageBroker.Publish(this, new ControlledPartyBehaviorUpdated(data));
    }

    public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(data.MobilePartyId, out MobileParty party))
                    return;

                bool isSelfEcho = ModInformation.IsClient &&
                    party.IsControlledByThisInstance() &&
                    !string.IsNullOrEmpty(data.OriginControllerId) &&
                    string.Equals(data.OriginControllerId, controllerIdProvider.ControllerId, StringComparison.Ordinal);

                bool isPredictionAcknowledgement = false;
                if (isSelfEcho && latestPredictions.TryGetValue(data.MobilePartyId, out var latestPrediction))
                {
                    if (!MatchesPrediction(data, latestPrediction))
                        return;

                    isPredictionAcknowledgement = true;
                }

                PartyBase partyBase = null;
                if (data.HasTarget && !objectManager.TryGetObjectWithLogging(data.InteractablePointId, out partyBase))
                    return;

                // The apply drives the Harmony-patched SetAiBehavior path; the AutoSync
                // patch must stand down while the replicated behavior is applied.
                using (new AllowedThread())
                {
                    PartyBehaviorPatch.SetAiBehavior(party.Ai, data.NewAiBehavior, partyBase, data.BestTargetPoint);

                    if (MobilePartyAiConfig.DEBUG)
                    {
                        Logger.Debug(
                            "Setting AI behavior. PartyId: {PartyId}, Behavior: {Behavior}, TargetParty: {TargetParty}, BestTargetPoint: {BestTargetPoint}",
                            data.MobilePartyId,
                            data.NewAiBehavior,
                            partyBase,
                            data.BestTargetPoint
                        );
                    }

                    if (ModInformation.IsClient)
                    {
                        // A matching prediction echo restores behavior without rewinding movement.
                        if (!isPredictionAcknowledgement)
                            party.Position = data.PartyPosition;
                    }
                    else
                    {
                        data.PartyPosition = party.Position;
                        messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(UpdatePartyBehavior));
            }
        });
    }

    internal static bool MatchesPrediction(PartyBehaviorUpdateData update, PartyBehaviorUpdateData prediction)
    {
        return update.NewAiBehavior == prediction.NewAiBehavior &&
            update.HasTarget == prediction.HasTarget &&
            string.Equals(update.InteractablePointId, prediction.InteractablePointId, StringComparison.Ordinal) &&
            update.BestTargetPoint.IsOnLand == prediction.BestTargetPoint.IsOnLand &&
            update.BestTargetPoint.X.Equals(prediction.BestTargetPoint.X) &&
            update.BestTargetPoint.Y.Equals(prediction.BestTargetPoint.Y);
    }
}