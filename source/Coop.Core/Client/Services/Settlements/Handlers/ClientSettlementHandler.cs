using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements;
using GameInterface.Services.Settlements.Messages;
using System;

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
        messageBroker.Subscribe<NetworkChangeSettlementLastThreatTime>(HandleLastThreatTime);
        messageBroker.Subscribe<NetworkChangeSettlementCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Subscribe<NetworkChangeSettlementMilitia>(HandleMiltia);
        messageBroker.Subscribe<NetworkChangeSettlementGarrisonWagePaymentLimit>(HandleGarrisonWageLimit);
        messageBroker.Subscribe<NetworkChangeSettlementNotablesCache>(HandleCollectNotablesToCache);
        messageBroker.Subscribe<NetworkChangeSettlementAddHeroWithoutParty>(HandleAddHeroWithoutParty);
        messageBroker.Subscribe<NetworkChangeSettlementRemoveHeroWithoutParty>(HandleRemoveHeroWithoutParty);
        messageBroker.Subscribe<NetworkChangeSettlementMobileParty>(HandleMobileParty);

        messageBroker.Subscribe<NetworkChangeWallHitPointsRatio>(HandleHitPointsRatio);


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

        var message = new ChangeMobileParty(obj.SettlementId, obj.MobilePartyId, obj.NumberOfLordParties, obj.AddMobileParty);
        messageBroker.Publish(this, message);

    }

    private void HandleRemoveHeroWithoutParty(MessagePayload<NetworkChangeSettlementRemoveHeroWithoutParty> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementHeroWithoutPartyRemove(obj.SettlementId, obj.HeroId);

        messageBroker.Publish(this, message);
    }

    private void HandleAddHeroWithoutParty(MessagePayload<NetworkChangeSettlementAddHeroWithoutParty> payload)
    {
        var obj = payload.What;

        var message = new ChangeSettlementHeroWithoutParty(obj.SettlementId, obj.HeroId);

        messageBroker.Publish(this, message);
    }

    private void HandleCollectNotablesToCache(MessagePayload<NetworkChangeSettlementNotablesCache> payload)
    {
        var obj = payload.What;
        var message = new ChangeSettlementNotablesCache(obj.SettlementId, obj.NotablesCache);
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
        messageBroker.Unsubscribe<NetworkChangeSettlementLastThreatTime>(HandleLastThreatTime);
        messageBroker.Unsubscribe<NetworkChangeSettlementCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Unsubscribe<NetworkChangeSettlementMilitia>(HandleMiltia);
        messageBroker.Unsubscribe<NetworkChangeSettlementGarrisonWagePaymentLimit>(HandleGarrisonWageLimit);
        messageBroker.Unsubscribe<NetworkChangeSettlementNotablesCache>(HandleCollectNotablesToCache);
        messageBroker.Unsubscribe<NetworkChangeSettlementMobileParty>(HandleMobileParty);

    }
}
