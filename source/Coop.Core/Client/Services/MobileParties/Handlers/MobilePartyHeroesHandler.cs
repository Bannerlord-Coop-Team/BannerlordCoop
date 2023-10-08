using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using System;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

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
        messageBroker.Subscribe<AddHeroToParty>(Handle);
        messageBroker.Subscribe<NetworkAddHeroToPartyApproved>(Handle);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<AddHeroToParty>(Handle);
    }

    private void Handle(MessagePayload<AddHeroToParty> obj)
    {
        var payload = obj.What;

        NetworkAddHeroToPartyRequest addHeroToPartyRequest = new NetworkAddHeroToPartyRequest(payload.HeroId, payload.PartyId, payload.ShowNotification);

        network.SendAll(addHeroToPartyRequest);
    }

    private void Handle(MessagePayload<NetworkAddHeroToPartyApproved> obj)
    {
        var payload = obj.What;

        HeroAddedToParty heroAddedToParty = new HeroAddedToParty(payload.HeroId, payload.NewPartyId, payload.ShowNotification);

        messageBroker.Publish(this, heroAddedToParty);
    }
}