using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Settlements.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Audit;
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
        messageBroker.Subscribe<NetworkChangeSettlementLastAttackerParty>(HandleLastAttackerParty);
        messageBroker.Subscribe<NetworkChangeSettlementLastThreatTime>(HandleLastThreatTime);
        messageBroker.Subscribe<NetworkChangeSettlementCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Subscribe<NetworkChangeSettlementMilitia>(HandleMiltia);
        messageBroker.Subscribe<NetworkChangeSettlementGarrisonWagePaymentLimit>(HandleGarrisonWageLimit);
        messageBroker.Subscribe<NetworkChangeSettlementMobileParty>(HandleMobileParty);

        messageBroker.Subscribe<NetworkChangeWallHitPointsRatio>(HandleHitPointsRatio);
        messageBroker.Subscribe<NetworkChangeLastVisitTimeOfOwner>(HandleLastVisitTimeOfOwner);

        //client auditorNetworkSettlementAuditResults

        messageBroker.Subscribe<NetworkSettlementAuditResults>(HandleAuditor);

    }

    private void HandleAuditor(MessagePayload<NetworkSettlementAuditResults> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new SettlementAuditResponse(obj.Data, obj.ServerAuditResults));
    }

    private void HandleLastVisitTimeOfOwner(MessagePayload<NetworkChangeLastVisitTimeOfOwner> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSettlementLastVisitTimeOfOwner(obj.SettlementID,obj.CurrentTime));
    }

    private void HandleHitPointsRatio(MessagePayload<NetworkChangeWallHitPointsRatio> payload)
    {
        var obj = payload.What;
        var message = new ChangeSettlementWallHitPointsRatio(obj.SettlementId, obj.index, obj.hitPointsRatio);
        messageBroker.Publish(this, message);

    }

    private void HandleMobileParty(MessagePayload<NetworkChangeSettlementMobileParty> payload)
    {
        var obj = payload.What;

        var message = new ChangeMobileParty(obj.SettlementId, obj.MobilePartyId, obj.AddMobileParty);
        messageBroker.Publish(this, message);

    }

    private void HandleGarrisonWageLimit(MessagePayload<NetworkChangeSettlementGarrisonWagePaymentLimit> payload)
    {
        var obj = payload.What;
        var message = new ChangeSettlementGarrisonWagePaymentLimit(obj.SettlementId, obj.GarrisonWagePaymentLimit);
        messageBroker.Publish(this, message);
    }

    private void HandleMiltia(MessagePayload<NetworkChangeSettlementMilitia> payload)
    {
        var obj = payload.What;
        var message = new ChangeSettlementMilitia(obj.SettlementId, obj.Militia);
        messageBroker.Publish(this, message);
    }

    private void HandleCurrentSiegeState(MessagePayload<NetworkChangeSettlementCurrentSiegeState> payload)
    {
        var obj = payload.What;
        var message = new ChangeSettlementCurrentSiegeState(obj.SettlementId, obj.CurrentSiegeState);
        messageBroker.Publish(this, message);
    }

    private void HandleLastThreatTime(MessagePayload<NetworkChangeSettlementLastThreatTime> payload)
    {
        var obj = payload.What;
        var message = new ChangeSettlementLastThreatTime(obj.SettlementId, obj.LastThreatTimeTicks);
        messageBroker.Publish(this, message);

    }

    private void HandleLastAttackerParty(MessagePayload<NetworkChangeSettlementLastAttackerParty> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementLastAttackerParty(obj.SettlementId, obj.AttackerPartyId);

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
        messageBroker.Unsubscribe<NetworkChangeSettlementLastAttackerParty>(HandleLastAttackerParty);
        messageBroker.Unsubscribe<NetworkChangeSettlementLastThreatTime>(HandleLastThreatTime);
        messageBroker.Unsubscribe<NetworkChangeSettlementCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Unsubscribe<NetworkChangeSettlementMilitia>(HandleMiltia);
        messageBroker.Unsubscribe<NetworkChangeSettlementGarrisonWagePaymentLimit>(HandleGarrisonWageLimit);
        messageBroker.Unsubscribe<NetworkChangeSettlementMobileParty>(HandleMobileParty);
        messageBroker.Unsubscribe<NetworkChangeWallHitPointsRatio>(HandleHitPointsRatio);
        messageBroker.Unsubscribe<NetworkChangeLastVisitTimeOfOwner>(HandleLastVisitTimeOfOwner);

        messageBroker.Unsubscribe<NetworkSettlementAuditResults>(HandleAuditor);
    }
}
