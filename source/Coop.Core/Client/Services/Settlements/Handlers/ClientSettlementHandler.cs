using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Settlements.Handlers;
internal class ClientSettlementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientSettlementHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkChangeSettlementEnemiesSpotted>(HandleNumberOfEnemiesSpottedAround);
        messageBroker.Subscribe<NetworkChangeSettlementAlliesSpotted>(HandleNumberOfAlliesSpottedAround);
        messageBroker.Subscribe<NetworkChangeSettlementBribePaid>(HandleBribePaid);


    }

    private void HandleBribePaid(MessagePayload<NetworkChangeSettlementBribePaid> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementBribePaid(obj.SettlementId, obj.BribePaid);

        messageBroker.Publish(this, message);
    }

    private void HandleNumberOfAlliesSpottedAround(MessagePayload<NetworkChangeSettlementAlliesSpotted> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementAlliesSpotted(obj.SettlementId, obj.NumberOfAlliesSpottedAround);

        messageBroker.Publish(this, message);
    }

    private void HandleNumberOfEnemiesSpottedAround(MessagePayload<NetworkChangeSettlementEnemiesSpotted> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementEnemiesSpotted(obj.SettlementId, obj.NumberOfEnemiesSpottedAround);

        messageBroker.Publish(this, message);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeSettlementEnemiesSpotted>(HandleNumberOfEnemiesSpottedAround);
        messageBroker.Unsubscribe<NetworkChangeSettlementAlliesSpotted>(HandleNumberOfAlliesSpottedAround);
        messageBroker.Unsubscribe<NetworkChangeSettlementBribePaid>(HandleBribePaid);


    }
}
