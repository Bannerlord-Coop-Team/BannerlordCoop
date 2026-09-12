using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.TradeSkills.Data;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Inventory.TradeSkills.Handlers;

internal class TradeRumorsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TradeRumorsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionTradePlayerDataInterface sessionTradePlayerDataInterface;

    public TradeRumorsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionTradePlayerDataInterface sessionTradePlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionTradePlayerDataInterface = sessionTradePlayerDataInterface;

        messageBroker.Subscribe<UpdateTradeRumors>(Handle_UpdateTradeRumors);
        messageBroker.Subscribe<NetworkUpdateTradeRumors>(Handle_NetworkUpdateTradeRumors);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateTradeRumors>(Handle_UpdateTradeRumors);
        messageBroker.Unsubscribe<NetworkUpdateTradeRumors>(Handle_NetworkUpdateTradeRumors);
    }

    private void Handle_UpdateTradeRumors(MessagePayload<UpdateTradeRumors> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(Hero.MainHero, out var mainHeroId)) return;

        // Pack trade rumors
        var tradeRumorsData = new List<TradeRumorData>();
        foreach (var tradeRumor in data.TradeRumors)
        {
            if (!PackTradeRumorData(tradeRumor, out var tradeRumorData)) continue;

            tradeRumorsData.Add(tradeRumorData);
        }

        // Pack entered settlements
        var enteredSettlementsData = new Dictionary<string, long>();
        foreach (var enteredSettlement in data.EnteredSettlements)
        {
            if (!objectManager.TryGetIdWithLogging(enteredSettlement.Key, out var enteredSettlementId)) continue;

            enteredSettlementsData[enteredSettlementId] = enteredSettlement.Value._numTicks;
        }

        var message = new NetworkUpdateTradeRumors(mainHeroId, tradeRumorsData, enteredSettlementsData);
        network.SendAll(message);
    }

    private void Handle_NetworkUpdateTradeRumors(MessagePayload<NetworkUpdateTradeRumors> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            sessionTradePlayerDataInterface.UpdatePlayerTradeRumors(data.PlayerHeroId, data.TradeRumors, data.EnteredSettlements);
        });
    }

    private bool PackTradeRumorData(TradeRumor tradeRumor, out TradeRumorData tradeRumorData)
    {
        tradeRumorData = null;

        if (!objectManager.TryGetIdWithLogging(tradeRumor.Settlement, out var settlementId)) return false;
        if (!objectManager.TryGetIdWithLogging(tradeRumor.ItemCategory, out var itemObjectId)) return false;

        tradeRumorData = new(
            tradeRumor.RumorEndTime._numTicks,
            settlementId,
            itemObjectId,
            tradeRumor.BuyPrice,
            tradeRumor.SellPrice);
        return true;
    }
}
