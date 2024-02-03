using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System;
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

        messageBroker.Subscribe<ChangeSettlementEnemiesSpotted>(HandleNumberOfEnemiesSpottedAround);
        messageBroker.Subscribe<ChangeSettlementAlliesSpotted>(HandleNumberOfAlliesSpottedAround);
        messageBroker.Subscribe<ChangeSettlementBribePaid>(HandleBribePaid);


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

    private void HandleNumberOfAlliesSpottedAround(MessagePayload<ChangeSettlementAlliesSpotted> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Village ({SettlementId})", obj.SettlementId);
            return;
        }
        EntitiesSpottedSettlementPatch.RunNumberOfAlliesSpottedChange(settlement, obj.NumberOfAlliesSpottedAround);

    }

    private void HandleNumberOfEnemiesSpottedAround(MessagePayload<ChangeSettlementEnemiesSpotted> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Village ({SettlementId})", obj.SettlementId);
            return;
        }

        EntitiesSpottedSettlementPatch.RunNumberOfEnemiesSpottedChange(settlement, obj.NumberOfEnemiesSpottedAround);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeSettlementEnemiesSpotted>(HandleNumberOfEnemiesSpottedAround);
        messageBroker.Unsubscribe<ChangeSettlementAlliesSpotted>(HandleNumberOfAlliesSpottedAround);
        messageBroker.Unsubscribe<ChangeSettlementBribePaid>(HandleBribePaid);

    }
}
