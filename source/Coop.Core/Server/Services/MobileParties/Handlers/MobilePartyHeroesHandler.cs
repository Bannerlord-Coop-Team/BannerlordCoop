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
public class MobilePartyHeroesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;

    private string controllerId => controllerIdProvider.ControllerId;

    public MobilePartyHeroesHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider) 
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkAddHeroToPartyRequest>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddHeroToPartyRequest>(Handle);
    }

    private void Handle(MessagePayload<NetworkAddHeroToPartyRequest> obj)
    {
        var payload = obj.What;

        HeroAddedToParty heroAddedToParty = new HeroAddedToParty(payload.HeroId, payload.NewPartyId, payload.ShowNotification);

        messageBroker.Publish(this, heroAddedToParty);

        NetworkAddHeroToPartyApproved heroToPartyApproved = new NetworkAddHeroToPartyApproved(payload.HeroId, payload.NewPartyId, payload.ShowNotification);

        network.SendAll(heroToPartyApproved);
    }
}