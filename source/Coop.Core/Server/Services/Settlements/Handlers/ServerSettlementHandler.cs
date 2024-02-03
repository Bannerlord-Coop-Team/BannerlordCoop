using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Messages;
using System;

namespace Coop.Core.Server.Services.Settlements.Handlers;

/// <summary>
/// Handles all the settlements changes.
/// </summary>
internal class ServerSettlementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerSettlementHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<SettlementChangedEnemiesSpotted>(HandleNumberOfEnemiesSpottedAround);
        messageBroker.Subscribe<SettlementChangeAlliesSpotted>(HandleNumberOfAlliesSpottedAround);
        messageBroker.Subscribe<SettlementChangedBribePaid>(HandleBribePaid);

    }

    private void HandleBribePaid(MessagePayload<SettlementChangedBribePaid> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementBribePaid(obj.SettlementId, obj.BribePaid);

        network.SendAll(message);
    }

    private void HandleNumberOfAlliesSpottedAround(MessagePayload<SettlementChangeAlliesSpotted> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementAlliesSpotted(obj.SettlementId, obj.NumberOfAlliesSpottedAround);

        network.SendAll(message);
    }

    private void HandleNumberOfEnemiesSpottedAround(MessagePayload<SettlementChangedEnemiesSpotted> payload)
    {
        var obj = payload.What;

        var message = new NetworkChangeSettlementEnemiesSpotted(obj.SettlementId, obj.NumberOfEnemiesSpottedAround);

        network.SendAll(message);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SettlementChangedEnemiesSpotted>(HandleNumberOfEnemiesSpottedAround);
        messageBroker.Unsubscribe<SettlementChangeAlliesSpotted>(HandleNumberOfAlliesSpottedAround);
        messageBroker.Unsubscribe<SettlementChangedBribePaid>(HandleBribePaid);

    }
}
