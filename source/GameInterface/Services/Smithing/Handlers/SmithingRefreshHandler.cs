using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;

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
            messageBroker.Subscribe<NetworkRefreshSmelting>(Handle);
            messageBroker.Subscribe<NetworkRefreshRefinement>(Handle);
            messageBroker.Subscribe<NetworkRefreshCraftingVM>(Handle);
            messageBroker.Subscribe<NetworkRefreshWeaponDesignVM>(Handle);

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
            messageBroker.Unsubscribe<NetworkRefreshSmelting>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshRefinement>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshCraftingVM>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshWeaponDesignVM>(Handle);
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

        private void Handle(MessagePayload<NetworkRefreshSmelting> obj)
        {
            currentSmeltingVM?.RefreshList();
            currentSmeltingVM?.RefreshValues();

            RefreshCraftingVM();
        }

        private void Handle(MessagePayload<NetworkRefreshRefinement> obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.What.CraftingHeroId, out Hero craftingHero)) return;

            currentRefinementVM?.RefreshRefinementActionsList(craftingHero);
            currentCraftingVM?.OnRefinementSelectionChange();

            RefreshCraftingVM();
        }

        private void Handle(MessagePayload<NetworkRefreshCraftingVM> obj)
        {
            RefreshCraftingVM();
        }

        private void Handle(MessagePayload<NetworkRefreshWeaponDesignVM> obj)
        {
            // Error, object reference not set to instance of object. Happens when another client also has a WeaponDesignVM open?
            currentWeaponDesignVM?.RefreshWeaponDesignMode(null);
        }

        private void RefreshCraftingVM()
        {
            currentCraftingVM?.UpdateAll();
            currentCraftingVM?.RefreshValues();
        }
    }
}
