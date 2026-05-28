using Common.Messaging;
using Coop.Core.Client.Services.Players.Messages;
using Coop.Core.Server.Connections;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Players.Handlers;

/// <summary>
/// Handles new NetworkRegisterPlayer from the server and then goes to the GameInterface.
/// </summary>
internal class ClientPlayerHandler : IHandler
{

    private IMessageBroker _messageBroker;
    private readonly IPlayerRegistry playerRegistry;

    public ClientPlayerHandler(IMessageBroker messageBroker, IPlayerRegistry playerRegistry)
    {
        _messageBroker = messageBroker;
        this.playerRegistry = playerRegistry;
        _messageBroker.Subscribe<NetworkRegisterPlayer>(Handle);
    }

    public void Dispose()
    {
        _messageBroker.Unsubscribe<NetworkRegisterPlayer>(Handle);
    }

    private void Handle(MessagePayload<NetworkRegisterPlayer> obj)
    {
        var player = obj.What.Player;

        playerRegistry.AddPlayer(player);
    }
}
