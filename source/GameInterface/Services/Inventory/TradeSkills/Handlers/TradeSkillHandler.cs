using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.TradeSkills.Handlers;

internal class TradeSkillHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<TradeSkillHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ITradeSkillCampaignBehaviorInterface tradeSkillCampaignBehaviorInterface;

    public TradeSkillHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ITradeSkillCampaignBehaviorInterface tradeSkillCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.tradeSkillCampaignBehaviorInterface = tradeSkillCampaignBehaviorInterface;
        messageBroker.Subscribe<NetworkUpdateTradeData>(Handle_NetworkUpdateTradeData);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkUpdateTradeData>(Handle_NetworkUpdateTradeData);
    }

    private void Handle_NetworkUpdateTradeData(MessagePayload<NetworkUpdateTradeData> obj)
    {
        var data = obj.What;

        // Locally update trade data on clients
        GameThread.RunSafe(() =>
        {
            if (!tradeSkillCampaignBehaviorInterface.TryGetTradeSkillBehavior(out var tradeSkillBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;

            if (playerHero != Hero.MainHero) return;

            if (data.IsTrading)
            {
                foreach (ValueTuple<ItemRosterElement, int> valueTuple in data.PurchasedItems)
                {
                    tradeSkillBehavior.ProcessPurchases(valueTuple.Item1, valueTuple.Item2);
                }
            }
            foreach (ValueTuple<ItemRosterElement, int> valueTuple2 in data.SoldItems)
            {
                tradeSkillBehavior.ProcessSales(valueTuple2.Item1, valueTuple2.Item2, data.IsTrading);
            }
        });
    }
}
