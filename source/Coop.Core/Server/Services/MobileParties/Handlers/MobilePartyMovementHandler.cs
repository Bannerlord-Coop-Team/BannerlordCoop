using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Packets;
using Coop.Core.Common.Services.MobileParties.Data;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages.Movement;
using Serilog;
using Serilog.Core;
using System;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles server communication related to party behavior synchronisation.
/// </summary>
/// <seealso cref="Client.Services.MobileParties.Handlers.MobilePartyMovementHandler"/>
/// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler"/>
public class MobilePartyMovementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    public MobilePartyMovementHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControlledEntityRegistry controlledEntityRegistry,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkPartyMovementRequested>(Handle_NetworkPartyMovementRequested);
        messageBroker.Subscribe<NetworkUpdatePartyMovement>(Handle_NetworkUpdatePartyMovement);
        messageBroker.Subscribe<TargetPositionUpdateAttempted>(Handle_TargetPositionUpdateAttempted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPartyMovementRequested>(Handle_NetworkPartyMovementRequested);
        messageBroker.Unsubscribe<NetworkUpdatePartyMovement>(Handle_NetworkUpdatePartyMovement);
        messageBroker.Unsubscribe<TargetPositionUpdateAttempted>(Handle_TargetPositionUpdateAttempted);
    }

    private void Handle_NetworkPartyMovementRequested(MessagePayload<NetworkPartyMovementRequested> obj)
    {
        var payload = obj.What;
        var controllerId = payload.MovementData.ControllerId;
        var partyId = payload.MovementData.PartyId;
        if (controlledEntityRegistry.IsControlledBy(controllerId, partyId) == false)
        {
            Logger.Error("Client Id {clientId} attempted to control party {partyId} when it was not granted control.", controllerId, partyId);
            return;
        }

        var movementData = obj.What.MovementData;
        ProcessMovementData(movementData);

        var packet = new UpdatePartyMovementPacket(movementData);

        network.SendAll(packet);
    }

    private void Handle_NetworkUpdatePartyMovement(MessagePayload<NetworkUpdatePartyMovement> obj)
    {
        var movementData = obj.What.MovementData;
        ProcessMovementData(movementData);
    }

    private void ProcessMovementData(MobilePartyMovementData movementData)
    {
        
        var movementType = movementData.MovementType;

        ICommand message = null;

        switch (movementType)
        {
            case MovementType.TargetParty:
                throw new NotImplementedException();
                break;
            case MovementType.TargetSettlement:
                throw new NotImplementedException();
                break;
            case MovementType.TargetPosition:
                message = new UpdateTargetPosition(movementData.TargetPartyId, movementData.TargetPosition);
                break;
        }

        messageBroker.Publish(this, message);
    }

    private void Handle_TargetPositionUpdateAttempted(MessagePayload<TargetPositionUpdateAttempted> obj)
    {
        var payload = obj.What;
        if (controlledEntityRegistry.IsControlledBy(controllerIdProvider.ControllerId, payload.PartyId) == false)
        {
            Logger.Error("Server tried to update movement of an uncontrolled party {partyId}", payload.PartyId);
            return;
        }

        messageBroker.Publish(this, new UpdateTargetPosition(payload.PartyId, payload.TargetPosition));

        var movementData = new MobilePartyMovementData(
            MovementType.TargetPosition,
            null,
            payload.PartyId,
            null,
            null,
            payload.TargetPosition);
        var packet = new UpdatePartyMovementPacket(movementData);

        network.SendAll(packet);
    }
}
