using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.MobileParties.Data;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages.Movement;
using Serilog;
using System;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Handles client communication related to party behavior synchronisation.
/// </summary>
/// <seealso cref="Server.Services.MobileParties.Handlers.MobilePartyMovementHandler">Server's Handler</seealso>
/// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler">Game Interface's Handler</seealso>
public class MobilePartyMovementHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    public MobilePartyMovementHandler(IMessageBroker messageBroker,
    INetwork network,
    IControlledEntityRegistry controlledEntityRegistry,
    IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkUpdatePartyMovement>(Handle_NetworkUpdatePartyMovement);
        messageBroker.Subscribe<TargetPositionUpdateAttempted>(Handle_TargetPositionUpdateAttempted);
    }

    private void Handle_NetworkUpdatePartyMovement(MessagePayload<NetworkUpdatePartyMovement> obj)
    {
        var movementData = obj.What.MovementData;
        var movementType = obj.What.MovementData.MovementType;
        var partyId = movementData.PartyId;

        switch (movementType)
        {
            case MovementType.TargetParty:
                throw new NotImplementedException();
                return;
            case MovementType.TargetSettlement:
                throw new NotImplementedException();
                return;
            case MovementType.TargetPosition:
                messageBroker.Publish(this, new UpdateTargetPosition(partyId, movementData.TargetPosition));
                return;
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TargetPositionUpdateAttempted>(Handle_TargetPositionUpdateAttempted);
    }

    private void Handle_TargetPositionUpdateAttempted(MessagePayload<TargetPositionUpdateAttempted> obj)
    {
        var payload = obj.What;
        var controllerId = controllerIdProvider.ControllerId;
        if (controlledEntityRegistry.IsControlledBy(controllerId, payload.PartyId) == false)
        {
            Logger.Error("Client attempted to move an uncontrolled party");
            return;
        }

        var movementData = new MobilePartyMovementData(
            MovementType.TargetPosition,
            controllerId,
            payload.PartyId,
            null,
            null,
            payload.TargetPosition);
        var packet = new RequestMobileMovementPacket(movementData);

        network.SendAll(packet);
    }
}
