using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Messages;

namespace Coop.Core.Client.Services.Settlements.Handlers;
internal class ClientSettlementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientSettlementHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkChangeSettlementBribePaid>(HandleBribePaid);
        messageBroker.Subscribe<NetworkChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Subscribe<NetworkChangeSettlementLastAttackerParty>(HandleLastAttackerParty);


    }

    private void HandleLastAttackerParty(MessagePayload<NetworkChangeSettlementLastAttackerParty> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementLastAttackerParty(obj.SettlementId, obj.AttackerPartyId);

        messageBroker.Publish(this, message);
    }

    private void HandleHitPoints(MessagePayload<NetworkChangeSettlementHitPoints> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementHitPoints(obj.SettlementId, obj.SettlementHitPoints);

        messageBroker.Publish(this, message);
    }

    private void HandleBribePaid(MessagePayload<NetworkChangeSettlementBribePaid> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementBribePaid(obj.SettlementId, obj.BribePaid);

        messageBroker.Publish(this, message);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeSettlementBribePaid>(HandleBribePaid);
        messageBroker.Unsubscribe<NetworkChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Unsubscribe<NetworkChangeSettlementLastAttackerParty>(HandleLastAttackerParty);



    }
}
