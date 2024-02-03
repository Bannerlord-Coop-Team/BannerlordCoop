using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers;
public class SettlementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SettlementHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeSettlementBribePaid>(HandleBribePaid);
        messageBroker.Subscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Subscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Subscribe<ChangeSettlementLastAttackerParty>(HandleLastAttackerParty);

    }

    private void HandleLastAttackerParty(MessagePayload<ChangeSettlementLastAttackerParty> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Village ({SettlementId})", obj.SettlementId);
            return;
        }
        if (objectManager.TryGetObject<MobileParty>(obj.AttackerPartyId, out var mobileParty) == false)
        {
            Logger.Error("Unable to find Village ({SettlementId})", obj.SettlementId);
            return;
        }

        LastAttackerPartySettlementPatch.RunLastAttackerPartyChange(settlement, mobileParty);

    }

    private void HandleHitPoints(MessagePayload<ChangeSettlementHitPoints> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Village ({SettlementId})", obj.SettlementId);
            return;
        }

        SettlementHitPointsPatch.RunSettlementHitPointsChange(settlement, obj.SettlementHitPoints);
    }

    private void HandleBribePaid(MessagePayload<ChangeSettlementBribePaid> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Village ({SettlementId})", obj.SettlementId);
            return;
        }
        BribePaidSettlementPatch.RunBribePaidChange(settlement, obj.BribePaid);
    }


    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeSettlementBribePaid>(HandleBribePaid);
        messageBroker.Unsubscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Unsubscribe<ChangeSettlementLastAttackerParty>(HandleLastAttackerParty);


    }
}
