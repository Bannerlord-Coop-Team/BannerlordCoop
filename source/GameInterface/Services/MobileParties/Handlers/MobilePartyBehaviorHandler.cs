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

    // Owner-party position resync threshold, in campaign-map units. The per-click prediction lead is
    // a small fraction of a unit while map features sit tens of units apart, so 1 unit stays above
    // normal prediction drift but still catches a genuine desync.
    private const float OwnerPositionResyncThreshold = 1f;

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

        messageBroker.Publish(this, new ControlledPartyBehaviorUpdated(data));
    }

    public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        GameThread.Run(() =>
        {
            try
            {
                PartyBase partyBase = null;
                if (data.HasTarget && !objectManager.TryGetObject(data.InteractablePointId, out partyBase))
                    return;

                if (!objectManager.TryGetObject(data.MobilePartyId, out MobileParty party))
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
                        // The local player's own party is client-predicted and runs slightly ahead of the
                        // server's relayed position; snapping it mid-move is the jitter. Snap when it's at rest,
                        // on a nav-layer (land/sea) change, or once it's drifted far enough to be a real desync.
                        if (!party.IsControlledByThisInstance() ||
                            !party.IsMoving ||
                            party.Position.IsOnLand != data.PartyPosition.IsOnLand ||
                            party.Position.Distance(data.PartyPosition) > OwnerPositionResyncThreshold)
                        {
                            party.Position = data.PartyPosition;
                        }
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
}