using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Villages.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Villages.Handlers;

/// <summary>
/// TODO describe class
/// </summary>
internal class ClientVillageStateHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientVillageStateHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        // This handles a message from the server
        messageBroker.Subscribe<ServerVillageChangeState>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ServerVillageChangeState>(Handle);
    }

    private void Handle(MessagePayload<ServerVillageChangeState> obj)
    {
        var payload = obj.What.VillageChange;

        // Changes the state on the client
        var message = new VillageChangeState(payload);
        messageBroker.Publish(this, message);
    }
}
