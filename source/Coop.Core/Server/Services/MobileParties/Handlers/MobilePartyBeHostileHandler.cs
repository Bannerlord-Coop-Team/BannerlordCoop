using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using System;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles heroes of mobile party entities.
/// </summary>
public class MobilePartyBeHostileHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;

    private string controllerId => controllerIdProvider.ControllerId;

    public MobilePartyBeHostileHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider) 
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkBecomeHostileRequest>(Handle);

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBecomeHostileRequest>(Handle);
    }


    private void Handle(MessagePayload<NetworkBecomeHostileRequest> obj)
    {
        var payload = obj.What;

        PartyBeHostile partyBeHostile = new PartyBeHostile(payload.AttackerPartyId, payload.DefenderPartyId, payload.Value);

        messageBroker.Publish(this, partyBeHostile);

        NetworkPartyBeHostileApproved hostileApproved = new NetworkPartyBeHostileApproved(payload.AttackerPartyId, payload.DefenderPartyId, payload.Value);

        network.SendAll(hostileApproved);
    }
}