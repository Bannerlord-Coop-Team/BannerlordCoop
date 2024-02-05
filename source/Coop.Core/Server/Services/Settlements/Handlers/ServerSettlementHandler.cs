using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Messages;

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

        messageBroker.Subscribe<SettlementChangedBribePaid>(HandleBribePaid);
        messageBroker.Subscribe<SettlementChangedSettlementHitPoints>(HandleHitPoints);
        messageBroker.Subscribe<SettlementChangedLastAttackerParty>(HandleLastAttackerParty);
        messageBroker.Subscribe<SettlementChangedLastThreatTime>(HandleLastThreatTime);
        messageBroker.Subscribe<SettlementChangedCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Subscribe<SettlementChangedMilitia>(HandleMilitia);
        messageBroker.Subscribe<SettlementChangedGarrisonWageLimit>(HandleGarrisonWageLimit);

        
    }

    private void HandleGarrisonWageLimit(MessagePayload<SettlementChangedGarrisonWageLimit> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementGarrisonWagePaymentLimit(obj.SettlementId, obj.GarrisonWagePaymentLimit);
        network.SendAll(message);

    }

    private void HandleMilitia(MessagePayload<SettlementChangedMilitia> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementMilitia(obj.SettlementId, obj.Militia);
        network.SendAll(message);
    }

    private void HandleCurrentSiegeState(MessagePayload<SettlementChangedCurrentSiegeState> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementCurrentSiegeState(obj.SettlementId, obj.CurrentSiegeState);
        network.SendAll(message);
    }

    private void HandleLastThreatTime(MessagePayload<SettlementChangedLastThreatTime> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementLastThreatTime(obj.SettlementId, obj.LastThreatTimeTicks);
        network.SendAll(message);
    }

    private void HandleLastAttackerParty(MessagePayload<SettlementChangedLastAttackerParty> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementLastAttackerParty(obj.SettlementId, obj.AttackerPartyId);

        network.SendAll(message);   
    }

    private void HandleHitPoints(MessagePayload<SettlementChangedSettlementHitPoints> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementHitPoints(obj.SettlementId, obj.SettlementHitPoints);

        network.SendAll(message);
    }

    private void HandleBribePaid(MessagePayload<SettlementChangedBribePaid> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeSettlementBribePaid(obj.SettlementId, obj.BribePaid);

        network.SendAll(message);
    }


    public void Dispose()
    {
        messageBroker.Unsubscribe<SettlementChangedBribePaid>(HandleBribePaid);
        messageBroker.Unsubscribe<SettlementChangedSettlementHitPoints>(HandleHitPoints);
        messageBroker.Unsubscribe<SettlementChangedLastAttackerParty>(HandleLastAttackerParty);
        messageBroker.Unsubscribe<SettlementChangedLastThreatTime>(HandleLastThreatTime);
        messageBroker.Unsubscribe<SettlementChangedCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Unsubscribe<SettlementChangedMilitia>(HandleMilitia);
        messageBroker.Unsubscribe<SettlementChangedGarrisonWageLimit>(HandleGarrisonWageLimit);
    }
}