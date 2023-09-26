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

    public NewPlayerHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
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

        messageBroker.Publish(this, new RegisterNewPlayerHero(peer, heroData));
    }
}
