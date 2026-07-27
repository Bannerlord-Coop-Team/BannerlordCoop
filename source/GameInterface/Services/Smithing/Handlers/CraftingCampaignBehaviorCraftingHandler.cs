using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Smithing.Patches;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorCraftingHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorCraftingHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;

    public CraftingCampaignBehaviorCraftingHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<SmeltingDone>(Handle);
        messageBroker.Subscribe<NetworkDoSmelting>(Handle);
        messageBroker.Subscribe<RefinementDone>(Handle);
        messageBroker.Subscribe<NetworkDoRefinement>(Handle);
        messageBroker.Subscribe<CraftedWeaponInternallyCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalServer>(Handle);
        messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalClients>(Handle);
        messageBroker.Subscribe<NetworkAddCraftedItemToRoster>(Handle);

        messageBroker.Subscribe<NetworkSetHeroCraftingStamina>(Handle);

        messageBroker.Subscribe<AddSkillXpFromCrafting>(Handle);
        messageBroker.Subscribe<NetworkAddSkillXpFromCrafting>(Handle);
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
        messageBroker.Unsubscribe<NetworkAddCraftedItemToRoster>(Handle);

        messageBroker.Unsubscribe<NetworkSetHeroCraftingStamina>(Handle);

        messageBroker.Unsubscribe<AddSkillXpFromCrafting>(Handle);
        messageBroker.Unsubscribe<NetworkAddSkillXpFromCrafting>(Handle);
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
        CreateCraftedWeaponInternalServer(obj.What);
    }

    private void Handle(MessagePayload<NetworkCreateCraftedWeaponInternalClients> obj)
    {
        CreateCraftedWeaponInternalClients(obj.What);
    }

    private void Handle(MessagePayload<NetworkAddCraftedItemToRoster> obj)
    {
        AddCraftedItemToRoster(obj.What);
    }

    private void Handle(MessagePayload<NetworkSetHeroCraftingStamina> obj)
    {
        SetHeroCraftingStaminaClients(obj.What);
    }

    private void Handle(MessagePayload<AddSkillXpFromCrafting> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.CraftingHero, out var craftingHeroId)) return;

            network.SendAll(new NetworkAddSkillXpFromCrafting(craftingHeroId, obj.What.Xp));
        });
    }

    private void Handle(MessagePayload<NetworkAddSkillXpFromCrafting> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.CraftingHeroId, out var craftingHero)) return;

            craftingHero.AddSkillXp(DefaultSkills.Crafting, obj.What.Xp);
        });
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
        GameThread.RunSafe(() =>
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

            if (itemRoster.FindIndexOfElement(equipmentElement) < 0) return; // Needed to prevent spam clicking to smelt more than actually available
            itemRoster.AddToCounts(equipmentElement, -1);
            for (int i = 8; i >= 0; i--)
            {
                if (smeltingOutputForItem[i] != 0)
                {
                    itemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i), smeltingOutputForItem[i]);
                }
            }

            craftingHero.AddSkillXp(DefaultSkills.Crafting, (float)Campaign.Current.Models.SmithingModel.GetSkillXpForSmelting(equipmentElement.Item));

            int energyCostForSmelting = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmelting(item, craftingHero);
            int newHeroCraftingStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForSmelting;
            craftingCampaignBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina); // Run on server
            network.SendAll(new NetworkSetHeroCraftingStamina(obj.CraftingCampaignBehaviorId, obj.CraftingHeroId, newHeroCraftingStamina)); // Run on clients

            CampaignEventDispatcher.Instance.OnEquipmentSmeltedByHero(craftingHero, equipmentElement);

            objectManager.TryGetId(itemRoster, out var rosterId);
            var compactId = Compact(rosterId, typeof(ItemRoster));

            sendCoalescer?.FlushInstance(compactId, network);

            network.SendAll(new NetworkRefreshSmelting()); // Refresh client ViewModels
        });
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
        GameThread.RunSafe(() =>
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
                if (itemRoster.FindIndexOfElement(new EquipmentElement(craftingMaterialItem, null, null, false)) < 0) return; // Needed to prevent spam clicking to refine more than actually available
                itemRoster.AddToCounts(craftingMaterialItem, -formula.Input1Count);
            }
            if (formula.Input2Count > 0)
            {
                ItemObject craftingMaterialItem2 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Input2);
                if (itemRoster.FindIndexOfElement(new EquipmentElement(craftingMaterialItem2, null, null, false)) < 0) return; // Needed to prevent spam clicking to refine more than actually available
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

            craftingHero.AddSkillXp(DefaultSkills.Crafting, (float)Campaign.Current.Models.SmithingModel.GetSkillXpForRefining(ref formula));

            int energyCostForRefining = Campaign.Current.Models.SmithingModel.GetEnergyCostForRefining(ref formula, craftingHero);
            int newHeroCraftingStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForRefining;
            craftingCampaignBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina); // Run on server
            network.SendAll(new NetworkSetHeroCraftingStamina(obj.CraftingCampaignBehaviorId, obj.CraftingHeroId, newHeroCraftingStamina)); // Run on clients

            CampaignEventDispatcher.Instance.OnItemsRefined(craftingHero, formula);

            objectManager.TryGetId(itemRoster, out var rosterId);
            var compactId = Compact(rosterId, typeof(ItemRoster));

            sendCoalescer?.FlushInstance(compactId, network);

            network.SendAll(new NetworkRefreshRefinement(obj.CraftingHeroId)); // Refresh client ViewModels
        });
    }

    private void SendInternallyCreatedWeapon(CraftedWeaponInternallyCreated obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.WeaponDesign.Template, out var craftingTemplateId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.PlayerHero, out var playerHeroId)) return;

        // Need to add to object manager early for completing town orders
        //objectManager.AddExisting(obj.NextCraftedItemId, obj.CraftedItemObject);

        //byte[] craftedItemObjectData = itemObjectInterface.PackageItemObject(obj.CraftedItemObject);

        string itemModifierGroupId = null;
        if (obj.CraftingLogic.CurrentItemModifierGroup != null && !objectManager.TryGetIdWithLogging(obj.CraftingLogic.CurrentItemModifierGroup, out itemModifierGroupId)) return;

        string cultureId = null;
        if (obj.CultureObject != null && !objectManager.TryGetIdWithLogging(obj.CultureObject, out cultureId)) return;

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
            obj.Name,
            cultureId,
            craftingTemplateId,
            obj.WeaponDesign.WeaponName?.ToString() ?? "",
            weaponDesignElementCraftingPieceIds,
            weaponDesignElementScalePercentages,
            weaponModifierId,
            playerHeroId,
            itemModifierGroupId
        );
        network.SendAll(message);
    }

    private void CreateCraftedWeaponInternalServer(NetworkCreateCraftedWeaponInternalServer obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return;

            //ItemObject craftedItemObject = itemObjectInterface.UnpackItemObject(obj.CraftedItemObjectData);

            ItemModifierGroup itemModifierGroup = null;
            if (obj.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(obj.ItemModifierGroupId, out itemModifierGroup)) return;

            if (!GetUsedPieces(obj.WeaponDesignElementCraftingPieceIds, obj.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return;

            ItemModifier weaponModifier = null;
            if (obj.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.WeaponModifierId, out weaponModifier)) return;

            CultureObject culture = null;
            if (obj.CultureId != null && !objectManager.TryGetObjectWithLogging(obj.CultureId, out culture)) return;

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

            var craftedItemObject = CreateAndRegisterCraftedItem(weaponDesign, obj.Name, culture, itemModifierGroup, nextCraftedItemId);

            // Send required data to all clients
            NetworkCreateCraftedWeaponInternalClients message = new(obj, nextCraftedItemId);
            network.SendAll(message);

            /*
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
            */

            int energyCostForSmithing = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(craftedItemObject, craftingHero);
            int newHeroCraftingStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForSmithing;
            craftingCampaignBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina); // Run on server
            network.SendAll(new NetworkSetHeroCraftingStamina(obj.CraftingCampaignBehaviorId, obj.CraftingHeroId, newHeroCraftingStamina)); // Run on clients

            CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !obj.IsFreeMode);
        });
    }

    private void CreateCraftedWeaponInternalClients(NetworkCreateCraftedWeaponInternalClients obj)
    {
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
                if (!objectManager.TryGetObjectWithLogging(obj.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return;

                if (!objectManager.TryGetObjectWithLogging(obj.PlayerHeroId, out Hero playerHero)) return;

                //ItemObject craftedItemObject = itemObjectInterface.UnpackItemObject(obj.CraftedItemObjectData);

                ItemModifierGroup itemModifierGroup = null;
                if (obj.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(obj.ItemModifierGroupId, out itemModifierGroup)) return;

                if (!GetUsedPieces(obj.WeaponDesignElementCraftingPieceIds, obj.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return;

                ItemModifier weaponModifier = null;
                if (obj.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.WeaponModifierId, out weaponModifier)) return;

                CultureObject culture = null;
                if (obj.CultureId != null && !objectManager.TryGetObjectWithLogging(obj.CultureId, out culture)) return;

                // Replace original TaleWorlds implementation
                string nextCraftedItemId = obj.NextCraftedItemId;
                WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(obj.WeaponName), usedPieces);
                if (obj.IsFreeMode)
                {
                    weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
                }

                var craftedItemObject = CreateAndRegisterCraftedItem(weaponDesign, obj.Name, culture, itemModifierGroup, nextCraftedItemId);
                CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !obj.IsFreeMode);

                // Only run on associated client
                if (GameStateManager.Current.ActiveState is CraftingState currentState && playerHero == Hero.MainHero)
                {
                    currentState.CraftingLogic._craftedItemObject = craftedItemObject;

                    // Update client's WeaponDesignVM to be referencing this crafted item instead
                    messageBroker.Publish(this, new UpdateCraftedItem(craftedItemObject));

                    AddItemToHistoryPatch.OverrideAddItemToHistory(ref craftingCampaignBehavior, craftedItemObject);

                    if (!objectManager.TryGetIdWithLogging(craftedItemObject, out var craftedItemId)) return;

                    // Add to item rosters after the item has finished being created on clients
                    var message = new NetworkAddCraftedItemToRoster(craftedItemId, obj.PlayerHeroId, obj.IsFreeMode, obj.WeaponModifierId);
                    network.SendAll(message);
                }
                /*
                else // Need to update craftingCampaignBehavior._craftedItemCount for every other client
                {
                    //objectManager.AddExisting(nextCraftedItemId, craftedItemObject);
                    //craftingCampaignBehavior.GetNextCraftedItemId();
                    CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !obj.IsFreeMode);
                    //MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);
                }*/
            }
        });
    }

    private void AddCraftedItemToRoster(NetworkAddCraftedItemToRoster obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftedItemId, out ItemObject craftedItemObject)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.PlayerHeroId, out Hero playerHero)) return;

            ItemModifier weaponModifier = null;
            if (obj.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.WeaponModifierId, out weaponModifier)) return;

            var targetItemRoster = playerHero.PartyBelongedTo.ItemRoster;
            if (obj.IsFreeMode)
            {
                if (weaponModifier == null)
                {
                    targetItemRoster.AddToCounts(craftedItemObject, 1);
                }
                else
                {
                    EquipmentElement rosterElement = new EquipmentElement(craftedItemObject, weaponModifier, null, false);
                    targetItemRoster.AddToCounts(rosterElement, 1);
                }
            }
        });
    }

    private bool GetUsedPieces(List<string> craftingPieceIds, List<int> scalePercentages, out WeaponDesignElement[] usedPieces)
    {
        usedPieces = null;
        List<WeaponDesignElement> usedPiecesList = new();
        for (int i = 0; i < craftingPieceIds.Count; i++)
        {
            if (craftingPieceIds[i] == "")
            {
                usedPiecesList.Add(WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i));
                continue;
            }

            if (!objectManager.TryGetObjectWithLogging(craftingPieceIds[i], out CraftingPiece currentCraftingPiece)) return false;

            usedPiecesList.Add(new WeaponDesignElement(currentCraftingPiece, scalePercentages[i]));
        }

        usedPieces = usedPiecesList.ToArray();
        return true;
    }

    private ItemObject CreateAndRegisterCraftedItem(WeaponDesign weaponDesign, TextObject name, CultureObject culture, ItemModifierGroup itemModifierGroup, string craftedItemId)
    {
        ItemObject craftedItemObject = null;
        using (new AllowedThread())
        {
            craftedItemObject = new();
            ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);
            //craftedItemObject.ItemComponent = null; // Need to clear the generated item component from the client, otherwise get duplicate weapons in Crafting.Generateitem from Add() instead of a new list
            Crafting.GenerateItem(
                weaponDesign,
                name,
                culture,
                itemModifierGroup,
                ref craftedItemObject,
                craftedItemId);
        }

        objectManager.AddExisting(craftedItemId, craftedItemObject);
        MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);

        return craftedItemObject;
    }

    private void SetHeroCraftingStaminaClients(NetworkSetHeroCraftingStamina obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;

            craftingCampaignBehavior.GetRecordForCompanion(craftingHero).CraftingStamina = MathF.Max(0, obj.Value);
        });
    }
}
