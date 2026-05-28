using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

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

            currentInventoryVM = null;
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<InventoryVMCreated>(Handle_InventoryVMCreated);
            messageBroker.Unsubscribe<RefreshOtherInventory>(Handle_RefreshOtherInventory);
        }

        private void Handle_InventoryVMCreated(MessagePayload<InventoryVMCreated> obj)
        {
            currentInventoryVM = obj.What.InventoryVM;
        }

        private void Handle_RefreshOtherInventory(MessagePayload<RefreshOtherInventory> obj)
        {
            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.ItemRosterId, out var itemRoster)) return;

            // Only want to update if the client is looking at the changed roster
            if (currentInventoryVM?._inventoryLogic == null || itemRoster != currentInventoryVM._inventoryLogic._rosters[0]) return;

            // Rebuild LeftItemListVM only
            GameLoopRunner.RunOnMainThread(() =>
            {
                currentInventoryVM.IsRefreshed = false;
                currentInventoryVM.LeftItemListVM.Clear();

                foreach (var itemRosterElement in currentInventoryVM._inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory))
                {
                    var itemVM = new SPItemVM(currentInventoryVM._inventoryLogic, currentInventoryVM.MainCharacter.IsFemale, currentInventoryVM.CanCharacterUseItem(itemRosterElement), currentInventoryVM._usageType, itemRosterElement, InventoryLogic.InventorySide.OtherInventory, currentInventoryVM._inventoryLogic.GetCostOfItemRosterElement(itemRosterElement, InventoryLogic.InventorySide.OtherInventory), null);
                    currentInventoryVM.UpdateFilteredStatusOfItem(itemVM);
                    itemVM.IsLocked = (itemVM.InventorySide == InventoryLogic.InventorySide.PlayerInventory && currentInventoryVM.IsItemLocked(itemRosterElement));
                    currentInventoryVM.LeftItemListVM.Add(itemVM);
                }

                currentInventoryVM.RefreshInformationValues();
                currentInventoryVM.IsRefreshed = true;
            });
        }
    }
}
