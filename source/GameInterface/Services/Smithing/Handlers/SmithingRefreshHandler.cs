using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;

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

        public SmithingRefreshHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<SmeltingVMCreated>(Handle);
            messageBroker.Subscribe<RefinementVMCreated>(Handle);
            messageBroker.Subscribe<CraftingVMCreated>(Handle);
            messageBroker.Subscribe<NetworkRefreshSmelting>(Handle);
            messageBroker.Subscribe<NetworkRefreshRefinement>(Handle);

            currentSmeltingVM = null;
            currentRefinementVM = null;
            currentCraftingVM = null;
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SmeltingVMCreated>(Handle);
            messageBroker.Unsubscribe<RefinementVMCreated>(Handle);
            messageBroker.Unsubscribe<CraftingVMCreated>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshSmelting>(Handle);
            messageBroker.Unsubscribe<NetworkRefreshRefinement>(Handle);
        }

        private void Handle(MessagePayload<SmeltingVMCreated> obj)
        {
            Logger.Information("SmithingRefreshHandler SmeltingVMCreated");
            currentSmeltingVM = obj.What.SmeltingVM;
        }

        private void Handle(MessagePayload<RefinementVMCreated> obj)
        {
            Logger.Information("SmithingRefreshHandler RefinementVMCreated");
            currentRefinementVM = obj.What.RefinementVM;
        }

        private void Handle(MessagePayload<CraftingVMCreated> obj)
        {
            Logger.Information("SmithingRefreshHandler CraftingVMCreated");
            currentCraftingVM = obj.What.CraftingVM;
        }

        private void Handle(MessagePayload<NetworkRefreshSmelting> obj)
        {
            Logger.Information("SmithingRefreshHandler NetworkRefreshSmelting");

            if (currentRefinementVM == null)
            {
                Logger.Warning("SmithingRefreshHandler currentSmithingVM was null");
            }

            currentSmeltingVM?.RefreshList();
        }

        private void Handle(MessagePayload<NetworkRefreshRefinement> obj)
        {
            Logger.Information("SmithingRefreshHandler NetworkRefreshRefinement");
            if (!objectManager.TryGetObject(obj.What.CraftingHeroId, out Hero craftingHero))
            {
                Logger.Error("Unable to get object for craftingHeroId {id}", obj.What.CraftingHeroId);
                return;
            }

            if (currentRefinementVM == null)
            {
                Logger.Warning("SmithingRefreshHandler currentRefinementVM was null");
            }

            if (currentCraftingVM == null)
            {
                Logger.Warning("SmithingRefreshHandler currentCraftingVM was null");
            }

            currentRefinementVM?.RefreshRefinementActionsList(craftingHero);
            currentCraftingVM?.OnRefinementSelectionChange();
            currentCraftingVM?.UpdateAll();
        }
    }
}
