using Common.Logging;
using Common.Messaging;
using Common;
using Common.Network;
using Common.Util;
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
            messageBroker.Subscribe<NetworkUpdateCraftedItemHistory>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkUpdateCraftedItemHistory>(Handle);
        }

        private void Handle(MessagePayload<NetworkUpdateCraftedItemHistory> obj)
        {
            // Craft history is produced by the authoritative craft commit.
            // Never accept a client-provided absolute history on the server.
            if (ModInformation.IsServer) return;
            sessionCraftingPlayerDataInterface.UpdateCraftingHistory(
                obj.What.PlayerHeroId,
                obj.What.CraftedItemHistoryIds);
        }
    }
}
