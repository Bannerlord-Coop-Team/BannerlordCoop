using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

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

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMobilePartyInterface mobilePartyInterface;
    private readonly IObjectManager objectManager;

    public MobilePartyBehaviorHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControlledEntityRegistry controlledEntityRegistry,
        IControllerIdProvider controllerIdProvider,
        IMobilePartyInterface mobilePartyInterface,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controlledEntityRegistry = controlledEntityRegistry;
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
        var payload = obj.What;

        var controllerId = controllerIdProvider.ControllerId;
        var partyId = payload.Ai._mobileParty.StringId;

        if (!controlledEntityRegistry.IsControlledBy(controllerId, partyId))
        {
            Logger.Warning(
                "Client attempted to move non-controlled party {PartyId}. ControllerId={ControllerId}, NewBehavior={NewBehavior}",
                partyId,
                controllerId,
                payload.NewAiBehavior);
            return;
        }

        if (!objectManager.TryGetId(payload.Ai, out var mobilePartyAiId))
        {
            Logger.Error(
                "Failed to resolve object id for MobilePartyAi while handling behavior change. PartyId={PartyId}, ControllerId={ControllerId}, Ai={@Ai}",
                partyId,
                controllerId,
                payload.Ai);
            return;
        }

        if (payload.InteractablePoint is PartyBase partyBase)
        {
            if (!objectManager.TryGetId(partyBase, out var partyBaseId))
            {
                return;
            }
            var message = new UpdatePartyBehavior(partyId, payload.NewAiBehavior, partyBaseId, payload.BestTargetPoint, true);
            network.SendAll(message);
        }
        else if (payload.InteractablePoint is null)
        {
            var message = new UpdatePartyBehavior(partyId, payload.NewAiBehavior, null, payload.BestTargetPoint, false);
            network.SendAll(message);
        }
        else
        {
            Logger.Error(
                "Unsupported interactable point type during behavior change. PartyId={PartyId}, MobilePartyAiId={MobilePartyAiId}, NewBehavior={NewBehavior}, InteractablePointType={InteractablePointType}, InteractablePoint={@InteractablePoint}",
                partyId,
                mobilePartyAiId,
                payload.NewAiBehavior,
                payload.InteractablePoint.GetType().FullName,
                payload.InteractablePoint);
            return;
        }

        using (new AllowedThread())
        {
            payload.Ai.SetAiBehavior(payload.NewAiBehavior, payload.InteractablePoint, payload.BestTargetPoint);
        }
    }

    public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
    {
        var payload = obj.What;

        PartyBase targetPartyBase = null;
        if (payload.HasTarget)
        {
            if (!objectManager.TryGetObject(payload.InteractablePointId, out targetPartyBase))
            {
                return;
            }
        }

        if (!objectManager.TryGetObject(payload.MobilePartyAiId, out MobilePartyAi mobilePartyAi))
        {
            Logger.Error(
                "Failed to resolve MobilePartyAi from behavior update. MobilePartyAiId={MobilePartyAiId}, NewBehavior={NewBehavior}, HasTarget={HasTarget}, InteractablePointId={InteractablePointId}",
                payload.MobilePartyAiId,
                payload.NewAiBehavior,
                payload.HasTarget,
                payload.InteractablePointId);
            return;
        }

        using (new AllowedThread())
        {
            mobilePartyAi.SetAiBehavior(payload.NewAiBehavior, targetPartyBase, payload.BestTargetPoint);
        }
    }
}