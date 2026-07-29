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

namespace GameInterface.Services.Smithing.Handlers;

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

        messageBroker.Subscribe<SmeltingVMCreated>(Handle_SmeltingVMCreated);
        messageBroker.Subscribe<RefinementVMCreated>(Handle_RefinementVMCreated);
        messageBroker.Subscribe<CraftingVMCreated>(Handle_CraftingVMCreated);
        messageBroker.Subscribe<WeaponDesignVMCreated>(Handle_WeaponDesignVMCreated);

        messageBroker.Subscribe<RefreshWeaponDesignVM>(Handle_RefreshWeaponDesignVM);
        messageBroker.Subscribe<NetworkRefreshSmelting>(Handle_NetworkRefreshSmelting);
        messageBroker.Subscribe<NetworkRefreshRefinement>(Handle_NetworkRefreshRefinement);
        messageBroker.Subscribe<RefreshCraftingVM>(Handle_RefreshCraftingVM);

        messageBroker.Subscribe<CompleteOrderFromVM>(Handle_CompleteOrderFromVM);

        currentSmeltingVM = null;
        currentRefinementVM = null;
        currentCraftingVM = null;
        currentWeaponDesignVM = null;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SmeltingVMCreated>(Handle_SmeltingVMCreated);
        messageBroker.Unsubscribe<RefinementVMCreated>(Handle_RefinementVMCreated);
        messageBroker.Unsubscribe<CraftingVMCreated>(Handle_CraftingVMCreated);
        messageBroker.Unsubscribe<WeaponDesignVMCreated>(Handle_WeaponDesignVMCreated);

        messageBroker.Unsubscribe<RefreshWeaponDesignVM>(Handle_RefreshWeaponDesignVM);
        messageBroker.Unsubscribe<NetworkRefreshSmelting>(Handle_NetworkRefreshSmelting);
        messageBroker.Unsubscribe<NetworkRefreshRefinement>(Handle_NetworkRefreshRefinement);
        messageBroker.Unsubscribe<RefreshCraftingVM>(Handle_RefreshCraftingVM);

        messageBroker.Unsubscribe<CompleteOrderFromVM>(Handle_CompleteOrderFromVM);
    }

    private void Handle_SmeltingVMCreated(MessagePayload<SmeltingVMCreated> obj)
    {
        currentSmeltingVM = obj.What.SmeltingVM;
    }

    private void Handle_RefinementVMCreated(MessagePayload<RefinementVMCreated> obj)
    {
        currentRefinementVM = obj.What.RefinementVM;
    }

    private void Handle_CraftingVMCreated(MessagePayload<CraftingVMCreated> obj)
    {
        currentCraftingVM = obj.What.CraftingVM;
    }

    private void Handle_WeaponDesignVMCreated(MessagePayload<WeaponDesignVMCreated> obj)
    {
        currentWeaponDesignVM = obj.What.WeaponDesignVM;
    }

    private void Handle_RefreshWeaponDesignVM(MessagePayload<RefreshWeaponDesignVM> obj)
    {
        RefreshWeaponDesignVM(obj.What.Town);
    }

    private void Handle_NetworkRefreshSmelting(MessagePayload<NetworkRefreshSmelting> obj)
    {
        GameThread.RunSafe(() =>
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

    private void Handle_NetworkRefreshRefinement(MessagePayload<NetworkRefreshRefinement> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(obj.What.CraftingHeroId, out Hero craftingHero)) return;

            currentRefinementVM?.RefreshRefinementActionsList(craftingHero);
            currentCraftingVM?.OnRefinementSelectionChange();

            RefreshCraftingVM();
        });
    }

    private void Handle_RefreshCraftingVM(MessagePayload<RefreshCraftingVM> obj)
    {
        GameThread.RunSafe(() =>
        {
            RefreshCraftingVM();
        });
    }

    private void Handle_CompleteOrderFromVM(MessagePayload<CompleteOrderFromVM> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (currentWeaponDesignVM == null) return;

            currentWeaponDesignVM._craftingBehavior.CompleteOrder(
                Settlement.CurrentSettlement.Town,
                currentWeaponDesignVM.ActiveCraftingOrder.CraftingOrder,
                obj.What.CraftedItemObject,
                currentWeaponDesignVM._getCurrentCraftingHero().Hero);

            currentWeaponDesignVM.CraftedItemObject = null;
        });
    }

    private void RefreshCraftingVM()
    {
        currentCraftingVM?.RefreshValues();
        currentCraftingVM?.UpdateAll();
    }

    private void RefreshWeaponDesignVM(Town town)
    {
        if (Settlement.CurrentSettlement?.Town != town || currentCraftingVM == null || currentCraftingVM.IsInCraftingMode == false) return;

        GameThread.RunSafe(() =>
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
