using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorInitializationHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorInitializationHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface;

        private CraftingPlayerData craftingPlayerData;

        public CraftingCampaignBehaviorInitializationHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.sessionCraftingPlayerDataInterface = sessionCraftingPlayerDataInterface;
            messageBroker.Subscribe<InitializeClientCraftingData>(Handle);
            messageBroker.Subscribe<PlayerHeroChanged>(Handle);
            messageBroker.Subscribe<NetworkInitializeServerCraftingDataKeys>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<InitializeClientCraftingData>(Handle);
            messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
            messageBroker.Unsubscribe<NetworkInitializeServerCraftingDataKeys>(Handle);
        }

        private void Handle(MessagePayload<InitializeClientCraftingData> obj)
        {
            craftingPlayerData = obj.What.CraftingPlayerData;
        }

        // Need to load crafting data when the hero changes for the player
        private void Handle(MessagePayload<PlayerHeroChanged> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

            CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();

            craftingCampaignBehavior._openedPartsDictionary = GetOpenedPartsDictionary(playerHeroId);
            craftingCampaignBehavior._openNewPartXpDictionary = GetOpenNewPartXpDictionary(playerHeroId);
            craftingCampaignBehavior._cratingItemsHistory = GetCraftedItemsHistory(playerHeroId);

            craftingCampaignBehavior.InitializeLists();

            network.SendAll(new NetworkInitializeServerCraftingDataKeys(playerHeroId));
        }

        private void Handle(MessagePayload<NetworkInitializeServerCraftingDataKeys> obj)
        {
            sessionCraftingPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
        }

        private Dictionary<CraftingTemplate, List<CraftingPiece>> GetOpenedPartsDictionary(string playerHeroId)
        {
            Dictionary<CraftingTemplate, List<CraftingPiece>> openedPartsDictionary = new Dictionary<CraftingTemplate, List<CraftingPiece>>();

            // Null and key check for players without existing crafting data
            if (craftingPlayerData?.PlayerOpenedPartsDictionary?.ContainsKey(playerHeroId) != true) return openedPartsDictionary;

            foreach (KeyValuePair<string, List<string>> openedPart in craftingPlayerData.PlayerOpenedPartsDictionary[playerHeroId])
            {
                if (!objectManager.TryGetObjectWithLogging(openedPart.Key, out CraftingTemplate currentCraftingTemplate)) continue;

                List<CraftingPiece> currentCraftingPieces = new List<CraftingPiece>();
                foreach (string craftingPieceId in openedPart.Value)
                {
                    if (!objectManager.TryGetObjectWithLogging(craftingPieceId, out CraftingPiece currentCraftingPiece)) continue;
                    currentCraftingPieces.Add(currentCraftingPiece);
                }

                openedPartsDictionary[currentCraftingTemplate] = currentCraftingPieces;
            }
            return openedPartsDictionary;
        }

        private Dictionary<CraftingTemplate, float> GetOpenNewPartXpDictionary(string playerHeroId)
        {
            Dictionary<CraftingTemplate, float> openNewPartXpDictionary = new Dictionary<CraftingTemplate, float>();

            // Null and key check for players without existing crafting data
            if (craftingPlayerData?.PlayerOpenNewPartXpDictionary?.ContainsKey(playerHeroId) != true) return openNewPartXpDictionary;

            foreach (KeyValuePair<string, float> partXp in craftingPlayerData.PlayerOpenNewPartXpDictionary[playerHeroId])
            {
                if (!objectManager.TryGetObjectWithLogging(partXp.Key, out CraftingTemplate currentCraftingTemplate)) continue;

                openNewPartXpDictionary[currentCraftingTemplate] = partXp.Value;
            }

            return openNewPartXpDictionary;
        }

        private List<ItemObject> GetCraftedItemsHistory(string playerHeroId)
        {
            List<ItemObject> craftedItemsHistory = new List<ItemObject>();

            // Null and key check for players without existing crafting data
            if (craftingPlayerData?.PlayerCraftedItemsHistory?.ContainsKey(playerHeroId) != true) return craftedItemsHistory;

            foreach (string itemId in craftingPlayerData.PlayerCraftedItemsHistory[playerHeroId])
            {
                if (!objectManager.TryGetObjectWithLogging(itemId, out ItemObject currentItemObject)) continue;

                craftedItemsHistory.Add(currentItemObject);
            }

            return craftedItemsHistory;
        }
    }
}
