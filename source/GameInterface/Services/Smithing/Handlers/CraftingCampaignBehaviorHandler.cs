using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IItemObjectInterface itemObjectInterface;

        public CraftingCampaignBehaviorHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IItemObjectInterface itemObjectInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.itemObjectInterface = itemObjectInterface;
            messageBroker.Subscribe<SmeltingDone>(Handle);
            messageBroker.Subscribe<NetworkDoSmelting>(Handle);
            messageBroker.Subscribe<RefinementDone>(Handle);
            messageBroker.Subscribe<NetworkDoRefinement>(Handle);
            messageBroker.Subscribe<CraftedWeaponInternallyCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalServer>(Handle);
            messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalClients>(Handle);

            messageBroker.Subscribe<NetworkSetHeroCraftingStamina>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SmeltingDone>(Handle);
            messageBroker.Unsubscribe<NetworkDoSmelting>(Handle);
            messageBroker.Unsubscribe<RefinementDone>(Handle);
            messageBroker.Unsubscribe<NetworkDoRefinement>(Handle);
            messageBroker.Unsubscribe<CraftedWeaponInternallyCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalServer>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalClients>(Handle);

            messageBroker.Unsubscribe<NetworkSetHeroCraftingStamina>(Handle);
        }

        private void Handle(MessagePayload<SmeltingDone> obj)
        {
            SendSmeltingDone(obj.What);
            //messageBroker.Publish(this, new RefreshSmelting());
        }

        private void Handle(MessagePayload<NetworkDoSmelting> obj)
        {
            DoSmelting(obj.What);
        }

        private void Handle(MessagePayload<RefinementDone> obj)
        {
            SendRefinementDone(obj.What);
            //messageBroker.Publish(this, new RefreshRefinement(obj.What.CraftingHero));
        }

        private void Handle(MessagePayload<NetworkDoRefinement> obj)
        {
            DoRefinement(obj.What);
        }

        private void Handle(MessagePayload<CraftedWeaponInternallyCreated> obj)
        {
            SendInternallyCreatedWeapon(obj.What);
        }

        private void Handle(MessagePayload<NetworkCreateCraftedWeaponInternalServer> obj)
        {
            // Send required data to all clients
            NetworkCreateCraftedWeaponInternalClients message = new(obj.What.CraftingCampaignBehaviorId, obj.What.CraftedItemObjectData, obj.What.NextCraftedItemId, obj.What.WeaponModifierId, obj.What.IsFreeMode);
            network.SendAll(message);

            CreateCraftedWeaponInternalServer(obj.What);
        }

        private void Handle(MessagePayload<NetworkCreateCraftedWeaponInternalClients> obj)
        {
            CreateCraftedWeaponInternalClients(obj.What);
        }

        private void Handle(MessagePayload<NetworkSetHeroCraftingStamina> obj)
        {
            SetHeroCraftingStaminaClients(obj.What);
        }

        private void SendSmeltingDone(SmeltingDone obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.CraftingHero, out var craftingHeroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingHero?.GetType());
                return;
            }

            // Can't send equipmentElement over the network as it is a struct. Need to reconstruct at the other end
            if (!objectManager.TryGetId(obj.EquipmentElement.Item, out var itemId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.EquipmentElement.Item?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.EquipmentElement.ItemModifier, out var itemModifierId))
            {
                itemModifierId = ""; // Assume EquipmentElement doesn't have an item modifier
            }
            if (!objectManager.TryGetId(obj.EquipmentElement.CosmeticItem, out var cosmeticItemId))
            {
                cosmeticItemId = ""; // Assume EquipmentElement doesn't have a cosmetic item
            }

            bool isQuestItem = obj.EquipmentElement.IsQuestItem;

            // Send to server from client
            NetworkDoSmelting message = new(
                craftingCampaignBehaviorId,
                craftingHeroId,
                itemId,
                itemModifierId,
                cosmeticItemId,
                isQuestItem
            );
            network.SendAll(message);
        }

        private void DoSmelting(NetworkDoSmelting obj)
        {
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CraftingHeroId, out Hero craftingHero))
            {
                Logger.Error("Unable to get object for craftingHeroId {id}", obj.CraftingHeroId);
                return;
            }
            if (!objectManager.TryGetObject(obj.ItemId, out ItemObject item))
            {
                Logger.Error("Unable to get object for itemId {id}", obj.ItemId);
                return;
            }
            ItemModifier itemModifier = null;
            if (obj.ItemModifierId != "" && !objectManager.TryGetObject(obj.ItemModifierId, out itemModifier))
            {
                Logger.Error("Unable to get object for itemModifierId {id}", obj.ItemModifierId);
                return;
            }
            ItemObject cosmeticItem = null;
            if (obj.CosmeticItemId != "" && !objectManager.TryGetObject(obj.CosmeticItemId, out cosmeticItem))
            {
                Logger.Error("Unable to get object for cosmeticItemId {id}", obj.CosmeticItemId);
                return;
            }

            // Rebuild equipmentElement on server
            var equipmentElement = new EquipmentElement(item, itemModifier, cosmeticItem, obj.IsQuestItem);

            // Replace original TaleWorlds implementation
            ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
            int[] smeltingOutputForItem = Campaign.Current.Models.SmithingModel.GetSmeltingOutputForItem(item);
            for (int i = 8; i >= 0; i--)
            {
                if (smeltingOutputForItem[i] != 0)
                {
                    itemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i), smeltingOutputForItem[i]);
                }
            }
            itemRoster.AddToCounts(equipmentElement, -1);

            int energyCostForSmelting = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmelting(item, craftingHero);
            int newHeroCraftingStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForSmelting;
            craftingCampaignBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina); // Run on server
            network.SendAll(new NetworkSetHeroCraftingStamina(obj.CraftingCampaignBehaviorId, obj.CraftingHeroId, newHeroCraftingStamina)); // Run on clients

            // Need to separately manage research points. Existing dictionary in CraftingCampaignBehavior won't work for multiple players
            craftingCampaignBehavior.AddResearchPoints(item.WeaponDesign.Template, Campaign.Current.Models.SmithingModel.GetPartResearchGainForSmeltingItem(item, craftingHero));

            CampaignEventDispatcher.Instance.OnEquipmentSmeltedByHero(craftingHero, equipmentElement);

            network.SendAll(new NetworkRefreshSmelting()); // Refresh client ViewModels
        }

        private void SendRefinementDone(RefinementDone obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.CraftingHero, out var craftingHeroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingHero?.GetType());
                return;
            }

            // Need to reconstruct formula at the other end
            Crafting.RefiningFormula formula = obj.RefiningFormula;

            // Send to server from client
            NetworkDoRefinement message = new(
                craftingCampaignBehaviorId,
                craftingHeroId,
                formula.Input1,
                formula.Input1Count,
                formula.Input2,
                formula.Input2Count,
                formula.Output,
                formula.OutputCount,
                formula.Output2,
                formula.Output2Count
            );
            network.SendAll(message);
        }

        private void DoRefinement(NetworkDoRefinement obj)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CraftingHeroId, out Hero craftingHero))
            {
                Logger.Error("Unable to get object for craftingHeroId {id}", obj.CraftingHeroId);
                return;
            }

            // Rebuild formula on server
            var formula = new Crafting.RefiningFormula(
                obj.Input1, obj.Input1Count,
                obj.Input2, obj.Input2Count,
                obj.Output1, obj.Output1Count,
                obj.Output2, obj.Output2Count);

            // Replace original TaleWorlds implementation
            ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
            if (formula.Input1Count > 0)
            {
                ItemObject craftingMaterialItem = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Input1);
                itemRoster.AddToCounts(craftingMaterialItem, -formula.Input1Count);
            }
            if (formula.Input2Count > 0)
            {
                ItemObject craftingMaterialItem2 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Input2);
                itemRoster.AddToCounts(craftingMaterialItem2, -formula.Input2Count);
            }
            if (formula.OutputCount > 0)
            {
                ItemObject craftingMaterialItem3 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Output);
                itemRoster.AddToCounts(craftingMaterialItem3, formula.OutputCount);
            }
            if (formula.Output2Count > 0)
            {
                ItemObject craftingMaterialItem4 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Output2);
                itemRoster.AddToCounts(craftingMaterialItem4, formula.Output2Count);
            }

            int energyCostForRefining = Campaign.Current.Models.SmithingModel.GetEnergyCostForRefining(ref formula, craftingHero);
            int newHeroCraftingStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForRefining;
            craftingCampaignBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina); // Run on server
            network.SendAll(new NetworkSetHeroCraftingStamina(obj.CraftingCampaignBehaviorId, obj.CraftingHeroId, newHeroCraftingStamina)); // Run on clients

            CampaignEventDispatcher.Instance.OnItemsRefined(craftingHero, formula);

            network.SendAll(new NetworkRefreshRefinement(obj.CraftingHeroId)); // Refresh client ViewModels
        }

        private void SendInternallyCreatedWeapon(CraftedWeaponInternallyCreated obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for Behavior instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.CraftingHero, out var craftingHeroId))
            {
                Logger.Error("Unable to get network ID for CraftingHero instance of type {type}", obj.CraftingHero?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.WeaponDesign.Template, out var craftingTemplateId))
            {
                Logger.Error("Unable to get network ID for CraftingTemplate instance of type {type}", obj.CraftingHero?.GetType());
                return;
            }

            byte[] craftedItemObjectData = itemObjectInterface.PackageItemObject(obj.CraftedItemObject);

            var weaponDesignElementCraftingPieceIds = new List<string>();
            var weaponDesignElementScalePercentages = new List<int>();
            foreach (var weaponDesignElement in obj.WeaponDesign._usedPieces)
            {
                if (!weaponDesignElement._craftingPiece.IsValid) // Skip invalid crafting pieces, e.g. Axe doesn't have a guard
                {
                    weaponDesignElementCraftingPieceIds.Add("");
                    weaponDesignElementScalePercentages.Add(-1);
                    continue;
                }

                if (!objectManager.TryGetId(weaponDesignElement._craftingPiece, out var currentCraftingPieceId))
                {
                    Logger.Error("Unable to get network ID for CraftingPiece instance of type {type}", weaponDesignElement._craftingPiece?.GetType());
                    return;
                }
                weaponDesignElementCraftingPieceIds.Add(currentCraftingPieceId);
                weaponDesignElementScalePercentages.Add(weaponDesignElement._scalePercentage);
            }

            var weaponModifierId = "";
            if (obj.WeaponModifier != null && !objectManager.TryGetId(obj.WeaponModifier, out weaponModifierId))
            {
                Logger.Error("Unable to get network ID for WeaponModifier instance of type {type}", obj.WeaponModifier?.GetType());
                return;
            }

            // Send to server from client
            NetworkCreateCraftedWeaponInternalServer message = new(
                craftingCampaignBehaviorId,
                obj.IsFreeMode,
                craftingHeroId,
                craftedItemObjectData,
                craftingTemplateId,
                obj.WeaponDesign.WeaponName?.ToString() ?? "",
                weaponDesignElementCraftingPieceIds,
                weaponDesignElementScalePercentages,
                weaponModifierId,
                obj.NextCraftedItemId
            );
            network.SendAll(message);
        }

        private void CreateCraftedWeaponInternalServer(NetworkCreateCraftedWeaponInternalServer obj)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CraftingHeroId, out Hero craftingHero))
            {
                Logger.Error("Unable to get object for craftingHeroId {id}", obj.CraftingHeroId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CraftingTemplateId, out CraftingTemplate craftingTemplate))
            {
                Logger.Error("Unable to get object for craftingTemplateId {id}", obj.CraftingTemplateId);
                return;
            }

            ItemObject craftedItemObject = itemObjectInterface.UnpackItemObject(obj.CraftedItemObjectData);

            List<WeaponDesignElement> usedPiecesList = new();
            for (int i = 0; i < obj.WeaponDesignElementCraftingPieceIds.Count; i++)
            {
                if (obj.WeaponDesignElementCraftingPieceIds[i] == "")
                {
                    usedPiecesList.Add(WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i));
                    continue;
                }

                if (!objectManager.TryGetObject(obj.WeaponDesignElementCraftingPieceIds[i], out CraftingPiece currentCraftingPiece))
                {
                    Logger.Error("Unable to get object for craftingTemplateId {id}", obj.CraftingTemplateId);
                    return;
                }

                usedPiecesList.Add(new WeaponDesignElement(currentCraftingPiece, obj.WeaponDesignElementScalePercentages[i]));
            }
            WeaponDesignElement[] usedPieces = usedPiecesList.ToArray();

            ItemModifier weaponModifier = null;
            if (obj.WeaponModifierId != "" && !objectManager.TryGetObject(obj.WeaponModifierId, out weaponModifier))
            {
                Logger.Error("Unable to get object for weaponModifierId {id}", obj.WeaponModifierId);
                return;
            }

            // Replace original TaleWorlds implementation

            string nextCraftedItemId = craftingCampaignBehavior.GetNextCraftedItemId();
            WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(obj.WeaponName), usedPieces);
            if (obj.IsFreeMode)
            {
                weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
            }

            // Implement CraftingCampaignBehavior.SpendMaterials(weaponDesign) here as it needs the party roster, MainParty on server won't be correct
            ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
            int[] smithingCostsForWeaponDesign = Campaign.Current.Models.SmithingModel.GetSmithingCostsForWeaponDesign(weaponDesign);
            for (int i = 8; i >= 0; i--)
            {
                if (smithingCostsForWeaponDesign[i] != 0)
                {
                    itemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i), smithingCostsForWeaponDesign[i]);
                }
            }

            ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);

            using (new AllowedThread())
            {
                craftedItemObject.StringId = nextCraftedItemId;
            }

            objectManager.AddExisting(nextCraftedItemId, craftedItemObject);
            MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);
            ItemObject registeredObject = MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);

            if (obj.IsFreeMode)
            {
                if (weaponModifier == null)
                {
                    itemRoster.AddToCounts(craftedItemObject, 1);
                }
                else
                {
                    EquipmentElement rosterElement = new EquipmentElement(craftedItemObject, weaponModifier, null, false);
                    itemRoster.AddToCounts(rosterElement, 1);
                }
            }

            int energyCostForSmithing = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(craftedItemObject, craftingHero);
            int newHeroCraftingStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForSmithing;
            craftingCampaignBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina); // Run on server
            network.SendAll(new NetworkSetHeroCraftingStamina(obj.CraftingCampaignBehaviorId, obj.CraftingHeroId, newHeroCraftingStamina)); // Run on clients

            // Need to separately manage research points. Existing dictionary in CraftingCampaignBehavior won't work for multiple players
            craftingCampaignBehavior.AddResearchPoints(weaponDesign.Template, Campaign.Current.Models.SmithingModel.GetPartResearchGainForSmithingItem(craftedItemObject, craftingHero, obj.IsFreeMode));

            CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !obj.IsFreeMode);
        }

        private void CreateCraftedWeaponInternalClients(NetworkCreateCraftedWeaponInternalClients obj)
        {
            string nextCraftedItemId = obj.NextCraftedItemId;
            ItemObject craftedItemObject = itemObjectInterface.UnpackItemObject(obj.CraftedItemObjectData);
            ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);
            using (new AllowedThread())
            {
                craftedItemObject.StringId = nextCraftedItemId;
            }

            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }

            ItemModifier weaponModifier = null;
            if (obj.WeaponModifierId != "" && !objectManager.TryGetObject(obj.WeaponModifierId, out weaponModifier))
            {
                Logger.Error("Unable to get object for weaponModifierId {id}", obj.WeaponModifierId);
                return;
            }

            if (GameStateManager.Current.ActiveState is CraftingState currentState && currentState.CraftingLogic._craftedItemObject.StringId == nextCraftedItemId) // Only run on associated client with matching id
            {
                // This line is adding duplicating the elements of craftedItemObject.Weapons, unsure if this causes any issues
                currentState.CraftingLogic.SetItemObject(craftedItemObject, nextCraftedItemId);
            }
            else // Need to update craftingCampaignBehavior._craftedItemCount for every other client. Manage with DynamicSync instead?
            {
                craftingCampaignBehavior.GetNextCraftedItemId();
                CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !obj.IsFreeMode);
                ItemObject registeredObject = MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);
                Logger.Information("Other client registered object with MBObjectManager with id: {id}", registeredObject.Id);
            }

            objectManager.AddExisting(nextCraftedItemId, craftedItemObject);
        }

        private void SetHeroCraftingStaminaClients(NetworkSetHeroCraftingStamina obj)
        {
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CraftingHeroId, out Hero craftingHero))
            {
                Logger.Error("Unable to get object for craftingHeroId {id}", obj.CraftingHeroId);
                return;
            }

            craftingCampaignBehavior.GetRecordForCompanion(craftingHero).CraftingStamina = MathF.Max(0, obj.Value);
        }
    }
}
