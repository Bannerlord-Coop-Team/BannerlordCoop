using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Interfaces;
using GameInterface.Services.Workshops.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Workshops.Handlers
{
    internal class WorkshopsCampaignBehaviorInitializationHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<WorkshopsCampaignBehaviorInitializationHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface;

        private WorkshopPlayerData workshopPlayerData;

        public WorkshopsCampaignBehaviorInitializationHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.sessionWorkshopPlayerDataInterface = sessionWorkshopPlayerDataInterface;
            messageBroker.Subscribe<InitializeClientWorkshopData>(Handle);
            messageBroker.Subscribe<PlayerHeroChanged>(Handle);
            messageBroker.Subscribe<NetworkInitializeServerWorkshopDataKeys>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<InitializeClientWorkshopData>(Handle);
            messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
            messageBroker.Unsubscribe<NetworkInitializeServerWorkshopDataKeys>(Handle);
        }

        private void Handle(MessagePayload<InitializeClientWorkshopData> obj)
        {
            workshopPlayerData = obj.What.WorkshopPlayerData;
        }

        // Need to load workshop data when the hero changes for the player
        private void Handle(MessagePayload<PlayerHeroChanged> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

            WorkshopsCampaignBehavior workshopsCampaignBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();

            workshopsCampaignBehavior._warehouseRosterPerSettlement = GetWarehouseRosterPerSettlement(playerHeroId);

            network.SendAll(new NetworkInitializeServerWorkshopDataKeys(playerHeroId));
        }

        private void Handle(MessagePayload<NetworkInitializeServerWorkshopDataKeys> obj)
        {
            sessionWorkshopPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
        }

        private KeyValuePair<Settlement, ItemRoster>[] GetWarehouseRosterPerSettlement(string playerHeroId)
        {
            int maxWorkshopCount = 0;
            using (new AllowedThread()) // Run in allowed thread to get original value only for client warehouse rosters
            {
                maxWorkshopCount = Campaign.Current.Models.WorkshopModel.MaximumWorkshopsPlayerCanHave;
            }
            
            KeyValuePair<Settlement, ItemRoster>[] warehouseRosterPerSettlement = new KeyValuePair<Settlement, ItemRoster>[maxWorkshopCount];
            
            // Null and key check for players without existing workshop data
            if (workshopPlayerData?.PlayerWarehouseRosterPerSettlement?.ContainsKey(playerHeroId) != true) return warehouseRosterPerSettlement;

            int index = 0;
            foreach (var settlementRoster in workshopPlayerData.PlayerWarehouseRosterPerSettlement[playerHeroId])
            {
                if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementRoster.Key, out var settlement)) continue;

                var itemRoster = new ItemRoster();
                foreach (var elementData in settlementRoster.Value)
                {
                    itemRoster.Add(sessionWorkshopPlayerDataInterface.GetItemRosterElementFromData(elementData));
                }

                warehouseRosterPerSettlement[index] = new(settlement, itemRoster);
                index++;
            }

            return warehouseRosterPerSettlement;
        }
    }
}
