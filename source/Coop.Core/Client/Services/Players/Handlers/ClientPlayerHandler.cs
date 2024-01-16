using Common.Messaging;
using Coop.Core.Client.Services.Players.Messages;
using GameInterface.Services.Players.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Players.Handlers;

internal class ClientPlayerHandler : IHandler
{

    private IMessageBroker _messageBroker;

    public ClientPlayerHandler(IMessageBroker messageBroker)
    {
        _messageBroker = messageBroker;
        _messageBroker.Subscribe<NetworkRegisterPlayer>(Handle);
    }

    private void Handle(MessagePayload<NetworkRegisterPlayer> obj)
    {
        var player = obj.What.Player;

        _messageBroker.Publish(this, new RegisterPlayer(player));
    }

    public void Dispose()
    {
        _messageBroker.Unsubscribe<NetworkRegisterPlayer>(Handle);
    }
}
