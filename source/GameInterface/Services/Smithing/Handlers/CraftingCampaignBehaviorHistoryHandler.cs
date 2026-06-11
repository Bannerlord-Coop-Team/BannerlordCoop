using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorHistoryHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorHistoryHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface;

        public CraftingCampaignBehaviorHistoryHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.sessionCraftingPlayerDataInterface = sessionCraftingPlayerDataInterface;
            messageBroker.Subscribe<CraftedItemHistoryUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateCraftedItemHistory>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CraftedItemHistoryUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateCraftedItemHistory>(Handle);
        }

        private void Handle(MessagePayload<CraftedItemHistoryUpdated> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out string playerHeroId)) return;

            List<string> craftedItemHistoryIds = new List<string>();
            foreach (ItemObject item in obj.What.CraftedItemHistory)
            {
                if (!objectManager.TryGetIdWithLogging(item, out string currentItemId)) return;
                craftedItemHistoryIds.Add(currentItemId);
            }
            
            network.SendAll(new NetworkUpdateCraftedItemHistory(playerHeroId, craftedItemHistoryIds));
        }

        private void Handle(MessagePayload<NetworkUpdateCraftedItemHistory> obj)
        {
            sessionCraftingPlayerDataInterface.UpdateCraftingHistory(
                obj.What.PlayerHeroId,
                obj.What.CraftedItemHistoryIds);
        }
    }
}
