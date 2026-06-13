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
using Serilog;
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

        string interactablePointId = null;
        if (interactablePoint is PartyBase partyBase &&
            !objectManager.TryGetId(partyBase, out interactablePointId))
            return;

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

        PartyBase partyBase = null;
        if (data.HasTarget && !objectManager.TryGetObject(data.InteractablePointId, out partyBase))
            return;

        if (!objectManager.TryGetObject(data.MobilePartyId, out MobileParty party))
            return;


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

            party.Ai.SetAiBehavior(data.NewAiBehavior, partyBase, data.BestTargetPoint);

            if (ModInformation.IsClient)
            {
                party.Ai._mobileParty.Position = data.PartyPosition;
            }
            else
            {
                data.PartyPosition = party.Position;
                messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
            }
        }
    }
}