using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Players.Messages;
using GameInterface.Services.Players.Messages;
using System;

namespace Coop.Core.Server.Services.Players.Handlers;


internal class ServerPlayerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerPlayerHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<PlayerRegistered>(Handle);
    }

    private void Handle(MessagePayload<PlayerRegistered> obj)
    {
        var player = obj.What.Player;
        network.SendAll(new NetworkRegisterPlayer(player)); 

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerRegistered>(Handle);
    }
}
