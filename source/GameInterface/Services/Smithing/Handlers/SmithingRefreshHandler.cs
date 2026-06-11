using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign.Order;
using TaleWorlds.Library;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class SmithingRefreshHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<SmithingRefreshHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        private SmeltingVM currentSmeltingVM;
        private RefinementVM currentRefinementVM;
        private CraftingVM currentCraftingVM;
        private WeaponDesignVM currentWeaponDesignVM;

        public SmithingRefreshHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<SmeltingVMCreated>(Handle);
            messageBroker.Subscribe<RefinementVMCreated>(Handle);
            messageBroker.Subscribe<CraftingVMCreated>(Handle);
            messageBroker.Subscribe<WeaponDesignVMCreated>(Handle);
            messageBroker.Subscribe<RefreshWeaponDesignVM>(Handle);
            messageBroker.Subscribe<NetworkRefreshSmelting>(Handle);
            messageBroker.Subscribe<NetworkRefreshRefinement>(Handle);
            messageBroker.Subscribe<RefreshCraftingVM>(Handle);

            currentSmeltingVM = null;
            currentRefinementVM = null;
            currentCraftingVM = null;
            currentWeaponDesignVM = null;
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SmeltingVMCreated>(Handle);
            messageBroker.Unsubscribe<RefinementVMCreated>(Handle);
            messageBroker.Unsubscribe<CraftingVMCreated>(Handle);
            messageBroker.Unsubscribe<WeaponDesignVMCreated>(Handle);
            messageBroker.Unsubscribe<RefreshWeaponDesignVM>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshSmelting>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshRefinement>(Handle);
            messageBroker.Unsubscribe<RefreshCraftingVM>(Handle);
        }

        private void Handle(MessagePayload<SmeltingVMCreated> obj)
        {
            currentSmeltingVM = obj.What.SmeltingVM;
        }

        private void Handle(MessagePayload<RefinementVMCreated> obj)
        {
            currentRefinementVM = obj.What.RefinementVM;
        }

        private void Handle(MessagePayload<CraftingVMCreated> obj)
        {
            currentCraftingVM = obj.What.CraftingVM;
        }

        private void Handle(MessagePayload<WeaponDesignVMCreated> obj)
        {
            currentWeaponDesignVM = obj.What.WeaponDesignVM;
        }

        private void Handle(MessagePayload<RefreshWeaponDesignVM> obj)
        {
            RefreshWeaponDesignVM(obj.What.Town);
        }

        private void Handle(MessagePayload<NetworkRefreshSmelting> obj)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                currentSmeltingVM?.RefreshValues();
                currentSmeltingVM?.RefreshList();

                if (currentSmeltingVM?.CurrentSelectedItem != null)
                {
                    int num = (int)(currentSmeltingVM?.SmeltableItemList.FindIndex((SmeltingItemVM i) => i.EquipmentElement.Item == currentSmeltingVM?.CurrentSelectedItem.EquipmentElement.Item));
                    SmeltingItemVM newItem = (num != -1) ? currentSmeltingVM?.SmeltableItemList[num] : currentSmeltingVM?.SmeltableItemList.FirstOrDefault<SmeltingItemVM>();
                    currentSmeltingVM?.OnItemSelection(newItem);
                }

                RefreshCraftingVM();
            });
        }

        private void Handle(MessagePayload<NetworkRefreshRefinement> obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.What.CraftingHeroId, out Hero craftingHero)) return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                currentRefinementVM?.RefreshRefinementActionsList(craftingHero);
                currentCraftingVM?.OnRefinementSelectionChange();

                RefreshCraftingVM();
            });
        }

        private void Handle(MessagePayload<RefreshCraftingVM> obj)
        {
            RefreshCraftingVM();
        }

        private void RefreshCraftingVM()
        {
            currentCraftingVM?.RefreshValues();
            currentCraftingVM?.UpdateAll();
        }

        private void RefreshWeaponDesignVM(Town town)
        {
            if (Settlement.CurrentSettlement?.Town != town || currentCraftingVM == null || currentCraftingVM.IsInCraftingMode == false) return;

            // Have to run on main thread to avoid UI related crashes
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    currentWeaponDesignVM?.CraftingOrderPopup?.RefreshOrders();
                    if (!(bool)(currentWeaponDesignVM?.IsInOrderMode))
                    {
                        currentWeaponDesignVM?.RefreshValues();
                        return;
                    }
    
                    CraftingOrderItemVM craftingOrderItemVM = currentWeaponDesignVM?.CraftingOrderPopup?.CraftingOrders?.FirstOrDefault((CraftingOrderItemVM x) => x.IsEnabled);
                    if (craftingOrderItemVM != null)
                    {
                        currentWeaponDesignVM?.CraftingOrderPopup?.SelectOrder(craftingOrderItemVM);
                    }
                    else
                    {
                        currentWeaponDesignVM?.ExecuteOpenFreeBuildTab();
                    }
                }
            });
        }
    }
}
