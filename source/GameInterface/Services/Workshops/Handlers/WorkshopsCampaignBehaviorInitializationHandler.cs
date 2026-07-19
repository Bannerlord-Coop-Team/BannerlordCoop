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
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

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
            Hero playerHero = obj.What.NewHero;
            if (!objectManager.TryGetIdWithLogging(playerHero, out string playerHeroId)) return;

            WorkshopsCampaignBehavior workshopsCampaignBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();

            workshopsCampaignBehavior._warehouseRosterPerSettlement = GetWarehouseRosterPerSettlement(playerHeroId, playerHero);

            network.SendAll(new NetworkInitializeServerWorkshopDataKeys(playerHeroId));
        }

        private void Handle(MessagePayload<NetworkInitializeServerWorkshopDataKeys> obj)
        {
            sessionWorkshopPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
        }

        private KeyValuePair<Settlement, ItemRoster>[] GetWarehouseRosterPerSettlement(string playerHeroId, Hero playerHero)
        {
            Dictionary<Settlement, ItemRoster> warehouseRosterPerSettlement = new();
            int maxWorkshopCount = 0;

            GameThread.RunSafe(() =>
            {
                using (new AllowedThread()) // Run in allowed thread to get original value only for client warehouse rosters
                {
                    maxWorkshopCount = Campaign.Current.Models.WorkshopModel.MaximumWorkshopsPlayerCanHave;
                }

                if (workshopPlayerData?.PlayerWarehouseRosterPerSettlement?.TryGetValue(playerHeroId, out var savedRosters) == true && savedRosters != null)
                {
                    foreach (var settlementRoster in savedRosters)
                    {
                        if (settlementRoster.Key == null || settlementRoster.Value == null)
                            continue;

                        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementRoster.Key, out var settlement)) continue;

                        var itemRoster = new ItemRoster();
                        foreach (var elementData in settlementRoster.Value)
                        {
                            ItemRosterElement rosterElement = sessionWorkshopPlayerDataInterface.GetItemRosterElementFromData(elementData);
                            if (rosterElement.EquipmentElement.Item == null) continue;

                            itemRoster.Add(rosterElement);
                        }

                        warehouseRosterPerSettlement[settlement] = itemRoster;
                    }
                }

                AddMissingOwnedWorkshopRosters(warehouseRosterPerSettlement, playerHero.OwnedWorkshops);
            }, blocking: true);

            return CreateWarehouseRosterSlots(warehouseRosterPerSettlement, maxWorkshopCount);
        }

        internal static void AddMissingOwnedWorkshopRosters(
            IDictionary<Settlement, ItemRoster> warehouseRosters,
            IEnumerable<Workshop> ownedWorkshops)
        {
            foreach (var workshop in ownedWorkshops)
            {
                Settlement settlement = workshop?.Settlement;
                if (settlement != null && !warehouseRosters.ContainsKey(settlement))
                {
                    warehouseRosters[settlement] = new ItemRoster();
                }
            }
        }

        internal static KeyValuePair<Settlement, ItemRoster>[] CreateWarehouseRosterSlots(
            IReadOnlyCollection<KeyValuePair<Settlement, ItemRoster>> warehouseRosters,
            int minimumCapacity)
        {
            int capacity = warehouseRosters.Count > minimumCapacity
                ? warehouseRosters.Count
                : minimumCapacity;
            var rosterSlots = new KeyValuePair<Settlement, ItemRoster>[capacity];
            int index = 0;

            foreach (var warehouseRoster in warehouseRosters)
            {
                rosterSlots[index++] = warehouseRoster;
            }

            return rosterSlots;
        }
    }
}
