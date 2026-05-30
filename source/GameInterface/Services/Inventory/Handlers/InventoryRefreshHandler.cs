using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Inventory.InventoryLogic;

namespace GameInterface.Services.Inventory.Handlers
{
    internal class InventoryRefreshHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<InventoryRefreshHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        private SPInventoryVM currentInventoryVM;

        public InventoryRefreshHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<InventoryVMCreated>(Handle_InventoryVMCreated);
            messageBroker.Subscribe<RefreshOtherInventory>(Handle_RefreshOtherInventory);
            messageBroker.Subscribe<RefreshAfterTrade>(Handle_RefreshAfterTrade);

            currentInventoryVM = null;
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<InventoryVMCreated>(Handle_InventoryVMCreated);
            messageBroker.Unsubscribe<RefreshOtherInventory>(Handle_RefreshOtherInventory);
            messageBroker.Unsubscribe<RefreshAfterTrade>(Handle_RefreshAfterTrade);
        }

        private void Handle_InventoryVMCreated(MessagePayload<InventoryVMCreated> obj)
        {
            currentInventoryVM = obj.What.InventoryVM;
        }

        private void Handle_RefreshOtherInventory(MessagePayload<RefreshOtherInventory> obj)
        {
            if (currentInventoryVM?._inventoryLogic == null) return;

            ItemRoster fromItemRoster = null;
            if (obj.What.FromItemRosterId != null && !objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.FromItemRosterId, out fromItemRoster)) return;

            ItemRoster toItemRoster = null;
            if (obj.What.ToItemRosterId != null && !objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.ToItemRosterId, out toItemRoster)) return;

            // Don't update if the client isn't looking at the changed roster
            if (fromItemRoster != currentInventoryVM._inventoryLogic._rosters[0] && toItemRoster != currentInventoryVM._inventoryLogic._rosters[0]) return;

            // Only update item costs for client who made the change
            if (fromItemRoster == currentInventoryVM._inventoryLogic._rosters[1] || toItemRoster == currentInventoryVM._inventoryLogic._rosters[1])
            {
                UpdateItemCosts(obj.What.EquipmentElement);
                return;
            }

            // Refresh the left item list for other clients looking at the same item roster
            GameLoopRunner.RunOnMainThread(() =>
            {
                currentInventoryVM.IsRefreshed = false;

                int itemRosterElementIndex = currentInventoryVM._inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory).FindIndex(rosterElement => rosterElement.EquipmentElement.Equals(obj.What.EquipmentElement));
                bool elementWasDeleted = itemRosterElementIndex < 0;

                SPItemVM targetItemVM = null;
                foreach (var itemVM in currentInventoryVM.LeftItemListVM)
                {
                    if (itemVM.ItemRosterElement.EquipmentElement.IsEqualTo(obj.What.EquipmentElement)) // Different amounts, can't compare ItemRosterElement directly
                    {
                        targetItemVM = itemVM;
                        break;
                    }
                }

                if (elementWasDeleted && targetItemVM != null) // Element deleted, remove VM
                {
                    currentInventoryVM.LeftItemListVM.Remove(targetItemVM);
                }
                else if (targetItemVM != null) // Existing element, update VM
                {
                    int amount = currentInventoryVM._inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)[itemRosterElementIndex].Amount;
                    targetItemVM.ItemRosterElement.Amount = amount;
                    targetItemVM.ItemCount = amount;
                    targetItemVM.RefreshWith(targetItemVM, InventorySide.OtherInventory);
                }
                else if (!elementWasDeleted) // New element, add a VM
                {
                    ItemRosterElement itemRosterElement = currentInventoryVM._inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)[itemRosterElementIndex];
                    var newItemVM = new SPItemVM(currentInventoryVM._inventoryLogic, currentInventoryVM.MainCharacter.IsFemale, currentInventoryVM.CanCharacterUseItem(itemRosterElement), currentInventoryVM._usageType, itemRosterElement, InventoryLogic.InventorySide.OtherInventory, currentInventoryVM._inventoryLogic.GetCostOfItemRosterElement(itemRosterElement, InventoryLogic.InventorySide.OtherInventory), null);
                    currentInventoryVM.UpdateFilteredStatusOfItem(newItemVM);
                    currentInventoryVM.LeftItemListVM.Add(newItemVM);

                    Tuple<int, int> currentOtherInventorySort = new((int)currentInventoryVM.OtherInventorySortController.CurrentSortOption.Value, (int)currentInventoryVM.OtherInventorySortController.CurrentSortState.Value);
                    currentInventoryVM.OtherInventorySortController.SortByOption((SPInventorySortControllerVM.InventoryItemSortOption)currentOtherInventorySort.Item1, (SPInventorySortControllerVM.InventoryItemSortState)currentOtherInventorySort.Item2);
                }

                // Update the costs in all related SPItemVMs
                UpdateItemCosts(obj.What.EquipmentElement);

                currentInventoryVM.RefreshInformationValues();
                currentInventoryVM.RefreshValues();
                currentInventoryVM.IsRefreshed = true;
            });
        }

        private void UpdateItemCosts(EquipmentElement equipmentElement)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                currentInventoryVM.UpdateCostOfItemsInCategory(new HashSet<ItemCategory> { equipmentElement.Item.GetItemCategory() });
            });
        }

        private void Handle_RefreshAfterTrade(MessagePayload<RefreshAfterTrade> obj)
        {
            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.ToItemRosterId, out var toItemRoster)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.FromItemRosterId, out var fromItemRoster)) return;

            if (currentInventoryVM?._inventoryLogic == null) return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                currentInventoryVM.IsRefreshed = false;

                // Update gold count for player inventory
                currentInventoryVM.RightInventoryOwnerGold = Hero.MainHero.Gold;

                // Update gold count for other inventory
                if (currentInventoryVM._inventoryLogic.LeftRosterName == null)
                {
                    Settlement settlement = currentInventoryVM._currentCharacter.HeroObject.CurrentSettlement;
                    if (settlement != null && currentInventoryVM._inventoryLogic.InventoryListener != null)
                    {
                        currentInventoryVM.LeftInventoryOwnerGold = currentInventoryVM._inventoryLogic.InventoryListener.GetGold();
                    }
                    else
                    {
                        PartyBase oppositePartyFromListener = currentInventoryVM._inventoryLogic.OppositePartyFromListener;
                        MobileParty mobileParty = oppositePartyFromListener?.MobileParty;
                        if (mobileParty != null && (mobileParty.IsCaravan || mobileParty.IsVillager))
                        {
                            InventoryListener inventoryListener = currentInventoryVM._inventoryLogic.InventoryListener;
                            currentInventoryVM.LeftInventoryOwnerGold = ((inventoryListener != null) ? inventoryListener.GetGold() : 0);
                        }
                    }
                }

                currentInventoryVM.IsRefreshed = true;
            });
        }
    }
}
