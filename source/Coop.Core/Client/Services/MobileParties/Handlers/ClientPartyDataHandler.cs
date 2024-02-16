using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages.Data;
using Coop.Core.Client.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MobileParties.Handlers;
internal class ClientPartyDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientPartyDataHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkChangePartyArmy>(Handle_NetworkChangeArmy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangePartyArmy>(Handle_NetworkChangeArmy);
    }

    private void Handle_NetworkChangeArmy(MessagePayload<NetworkChangePartyArmy> payload)
    {
        messageBroker.Publish(this, new ChangePartyArmy(payload.What.Data));
    }
}
