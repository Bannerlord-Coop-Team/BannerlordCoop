using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Smithing.Patches;
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
    internal class CraftingCampaignBehaviorCraftingHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorCraftingHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IItemObjectInterface itemObjectInterface;
        private readonly ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface;

        public CraftingCampaignBehaviorCraftingHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IBinaryPackageFactory binaryPackageFactory,
            IItemObjectInterface itemObjectInterface,
            ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.binaryPackageFactory = binaryPackageFactory;
            this.itemObjectInterface = itemObjectInterface;
            this.sessionCraftingPlayerDataInterface = sessionCraftingPlayerDataInterface;
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
        }

        private void Handle(MessagePayload<NetworkDoSmelting> obj)
        {
            DoSmelting(obj.What);
        }

        private void Handle(MessagePayload<RefinementDone> obj)
        {
            SendRefinementDone(obj.What);
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
            if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;

            // Can't send equipmentElement over the network as it is a struct. Need to reconstruct at the other end
            if (!objectManager.TryGetIdWithLogging(obj.EquipmentElement.Item, out var itemId)) return;
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
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.ItemId, out ItemObject item)) return;

            ItemModifier itemModifier = null;
            if (obj.ItemModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.ItemModifierId, out itemModifier)) return;

            ItemObject cosmeticItem = null;
            if (obj.CosmeticItemId != "" && !objectManager.TryGetObjectWithLogging(obj.CosmeticItemId, out cosmeticItem)) return;

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

            CampaignEventDispatcher.Instance.OnEquipmentSmeltedByHero(craftingHero, equipmentElement);

            network.SendAll(new NetworkRefreshSmelting()); // Refresh client ViewModels
        }

        private void SendRefinementDone(RefinementDone obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;

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
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;

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
            if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.WeaponDesign.Template, out var craftingTemplateId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.PlayerHero, out var playerHeroId)) return;

            // Failed to retrieve ID for object TaleWorlds.Core.ItemObject because not registered in ObjectManager yet, shouldn't cause any issues
            byte[] craftedItemObjectData = itemObjectInterface.PackageItemObject(obj.CraftedItemObject);

            CraftingBinaryPackage package = binaryPackageFactory.GetBinaryPackage<CraftingBinaryPackage>(obj.CraftingLogic);
            byte[] craftingLogicData = BinaryFormatterSerializer.Serialize(package);

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

                if (!objectManager.TryGetIdWithLogging(weaponDesignElement._craftingPiece, out var currentCraftingPieceId)) return;
                weaponDesignElementCraftingPieceIds.Add(currentCraftingPieceId);
                weaponDesignElementScalePercentages.Add(weaponDesignElement._scalePercentage);
            }

            var weaponModifierId = "";
            if (obj.WeaponModifier != null && !objectManager.TryGetIdWithLogging(obj.WeaponModifier, out weaponModifierId)) return;

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
                obj.NextCraftedItemId,
                playerHeroId,
                craftingLogicData
            );
            network.SendAll(message);
        }

        private void CreateCraftedWeaponInternalServer(NetworkCreateCraftedWeaponInternalServer obj)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return;

            ItemObject craftedItemObject = itemObjectInterface.UnpackItemObject(obj.CraftedItemObjectData);

            CraftingBinaryPackage package = BinaryFormatterSerializer.Deserialize<CraftingBinaryPackage>(obj.CraftingLogicData);
            Crafting craftingLogic = package.Unpack<Crafting>(binaryPackageFactory);

            List<WeaponDesignElement> usedPiecesList = new();
            for (int i = 0; i < obj.WeaponDesignElementCraftingPieceIds.Count; i++)
            {
                if (obj.WeaponDesignElementCraftingPieceIds[i] == "")
                {
                    usedPiecesList.Add(WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i));
                    continue;
                }

                if (!objectManager.TryGetObjectWithLogging(obj.WeaponDesignElementCraftingPieceIds[i], out CraftingPiece currentCraftingPiece)) return;

                usedPiecesList.Add(new WeaponDesignElement(currentCraftingPiece, obj.WeaponDesignElementScalePercentages[i]));
            }
            WeaponDesignElement[] usedPieces = usedPiecesList.ToArray();

            ItemModifier weaponModifier = null;
            if (obj.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.WeaponModifierId, out weaponModifier)) return;

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
                Crafting.GenerateItem(
                weaponDesign,
                craftedItemObject.Name,
                craftedItemObject.Culture,
                craftingLogic.CurrentItemModifierGroup,
                ref craftedItemObject,
                nextCraftedItemId);
            }

            objectManager.AddExisting(nextCraftedItemId, craftedItemObject);
            MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);

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

            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;

            ItemModifier weaponModifier = null;
            if (obj.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.WeaponModifierId, out weaponModifier)) return;

            objectManager.AddExisting(nextCraftedItemId, craftedItemObject);

            if (GameStateManager.Current.ActiveState is CraftingState currentState && currentState.CraftingLogic._craftedItemObject.StringId == nextCraftedItemId) // Only run on associated client with matching id
            {
                // This line is duplicating the elements of craftedItemObject.Weapons, unsure if this causes any issues
                currentState.CraftingLogic.SetItemObject(craftedItemObject, nextCraftedItemId);

                AddItemToHistoryPatch.OverrideAddItemToHistory(ref craftingCampaignBehavior, craftedItemObject);
            }
            else // Need to update craftingCampaignBehavior._craftedItemCount for every other client
            {
                craftingCampaignBehavior.GetNextCraftedItemId();
                CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !obj.IsFreeMode);
                MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);
            }
        }

        private void SetHeroCraftingStaminaClients(NetworkSetHeroCraftingStamina obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;

            craftingCampaignBehavior.GetRecordForCompanion(craftingHero).CraftingStamina = MathF.Max(0, obj.Value);
        }
    }
}
