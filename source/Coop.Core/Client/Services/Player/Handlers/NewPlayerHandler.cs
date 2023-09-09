using Common.Messaging;
using Coop.Core.Client.Services.Player.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;

namespace Coop.Core.Client.Services.Player.Handlers;

internal class NewPlayerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;

    public NewPlayerHandler(IMessageBroker messageBroker, IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkNewPlayerData>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPlayerData>(Handle);
    }

    private void Handle(MessagePayload<NetworkNewPlayerData> obj)
    {
        byte[] heroData = obj.What.HeroData;
        var peer = obj.Who as NetPeer;
        var controllerId = controllerIdProvider.ControllerId;

        messageBroker.Publish(this, new RegisterNewPlayerHero(peer, controllerId, heroData));
    }
}
