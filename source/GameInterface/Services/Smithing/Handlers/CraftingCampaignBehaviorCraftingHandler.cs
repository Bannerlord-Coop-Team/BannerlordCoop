using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Smithing.Patches;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorCraftingHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorCraftingHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface;
    private readonly ISendCoalescer sendCoalescer;

    public CraftingCampaignBehaviorCraftingHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.craftingCampaignBehaviorInterface = craftingCampaignBehaviorInterface;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<DoSmelting>(Handle_DoSmelting);
        messageBroker.Subscribe<NetworkDoSmelting>(Handle_NetworkDoSmelting);
        messageBroker.Subscribe<NetworkRefreshSmelting>(Handle_NetworkRefreshSmelting);

        messageBroker.Subscribe<DoRefinement>(Handle_DoRefinement);
        messageBroker.Subscribe<NetworkDoRefinement>(Handle_NetworkDoRefinement);

        messageBroker.Subscribe<CreatedCraftedWeaponInternal>(Handle_CreatedCraftedWeaponInternal);
        messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalServer>(Handle_NetworkCreateCraftedWeaponInternalServer);
        messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalClients>(Handle_NetworkCreateCraftedWeaponInternalClients);

        messageBroker.Subscribe<NetworkSetHeroCraftingStamina>(Handle_NetworkSetHeroCraftingStamina);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DoSmelting>(Handle_DoSmelting);
        messageBroker.Unsubscribe<NetworkDoSmelting>(Handle_NetworkDoSmelting);
        messageBroker.Unsubscribe<NetworkRefreshSmelting>(Handle_NetworkRefreshSmelting);

        messageBroker.Unsubscribe<DoRefinement>(Handle_DoRefinement);
        messageBroker.Unsubscribe<NetworkDoRefinement>(Handle_NetworkDoRefinement);

        messageBroker.Unsubscribe<CreatedCraftedWeaponInternal>(Handle_CreatedCraftedWeaponInternal);
        messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalServer>(Handle_NetworkCreateCraftedWeaponInternalServer);
        messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalClients>(Handle_NetworkCreateCraftedWeaponInternalClients);

        messageBroker.Unsubscribe<NetworkSetHeroCraftingStamina>(Handle_NetworkSetHeroCraftingStamina);
    }

    private void Handle_DoSmelting(MessagePayload<DoSmelting> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingHero, out var craftingHeroId)) return;

        NetworkDoSmelting message = new(craftingHeroId, obj.What.EquipmentElement);
        network.SendAll(message);
    }

    private void Handle_NetworkDoSmelting(MessagePayload<NetworkDoSmelting> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Get required objects using interface & objectManager
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return;

            // Replace original TaleWorlds implementation
            var newHeroCraftingStamina = craftingCampaignBehaviorInterface.DoSmelting(
                craftingCampaignBehavior,
                craftingHero,
                data.EquipmentElement,
                out var smeltingSucceeded);

            // Update stamina on clients
            network.SendAll(new NetworkSetHeroCraftingStamina(data.CraftingHeroId, newHeroCraftingStamina));

            // Refresh client view model
            FlushCoalescer(craftingHero.PartyBelongedTo.ItemRoster);
            network.Send(obj.Who as NetPeer, new NetworkRefreshSmelting(data.CraftingHeroId, data.EquipmentElement, smeltingSucceeded));
        });
    }

    private void Handle_NetworkRefreshSmelting(MessagePayload<NetworkRefreshSmelting> obj)
    {
        if (!obj.What.SmeltingSucceeded) return;

        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.What.CraftingHeroId, out Hero craftingHero)) return;

            var equipmentElement = obj.What.EquipmentElement;
            craftingCampaignBehavior.AddResearchPoints(
                equipmentElement.Item.WeaponDesign.Template,
                Campaign.Current.Models.SmithingModel.GetPartResearchGainForSmeltingItem(equipmentElement.Item, craftingHero));
        });
    }

    private void Handle_DoRefinement(MessagePayload<DoRefinement> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingHero, out var craftingHeroId)) return;

        // Need to reconstruct formula at the other end
        Crafting.RefiningFormula formula = obj.What.RefiningFormula;

        NetworkDoRefinement message = new(
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

    private void Handle_NetworkDoRefinement(MessagePayload<NetworkDoRefinement> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Get required objects using interface & objectManager
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return;

            // Rebuild formula on server
            var formula = new Crafting.RefiningFormula(
                data.Input1, data.Input1Count,
                data.Input2, data.Input2Count,
                data.Output1, data.Output1Count,
                data.Output2, data.Output2Count);

            // Replace original TaleWorlds implementation
            var newHeroCraftingStamina = craftingCampaignBehaviorInterface.DoRefinement(craftingCampaignBehavior, craftingHero, formula);

            // Update stamina on clients
            network.SendAll(new NetworkSetHeroCraftingStamina(data.CraftingHeroId, newHeroCraftingStamina));

            // Refresh client view model
            FlushCoalescer(craftingHero.PartyBelongedTo.ItemRoster);
            network.Send(obj.Who as NetPeer, new NetworkRefreshRefinement(data.CraftingHeroId));
        });
    }

    private void Handle_CreatedCraftedWeaponInternal(MessagePayload<CreatedCraftedWeaponInternal> obj)
    {
        var data = obj.What;

        if (!TryCreateCraftingRequest(data, out var message))
        {
            messageBroker.Publish(this, new CreateCraftingResultPopup(null, false, data.ClientRequestId));
            return;
        }

        network.SendAll(message);
    }

    private bool TryCreateCraftingRequest(CreatedCraftedWeaponInternal data, out NetworkCreateCraftedWeaponInternalServer message)
    {
        message = default;

        if (!objectManager.TryGetIdWithLogging(data.CraftingHero, out var craftingHeroId)) return false;
        if (!objectManager.TryGetIdWithLogging(data.WeaponDesign.Template, out var craftingTemplateId)) return false;
        if (!objectManager.TryGetIdWithLogging(data.PlayerHero, out var playerHeroId)) return false;
        if (!objectManager.TryGetIdWithLogging(data.CurrentSettlement, out var currentSettlementId)) return false;

        string itemModifierGroupId = null;
        if (data.CraftingLogic.CurrentItemModifierGroup != null && !objectManager.TryGetIdWithLogging(data.CraftingLogic.CurrentItemModifierGroup, out itemModifierGroupId)) return false;

        string cultureId = null;
        if (data.CultureObject != null && !objectManager.TryGetIdWithLogging(data.CultureObject, out cultureId)) return false;

        if (!PackUsedPieces(data.WeaponDesign, out var craftingPieceIds, out var scalePercentages)) return false;

        var weaponModifierId = "";
        if (data.WeaponModifier != null && !objectManager.TryGetIdWithLogging(data.WeaponModifier, out weaponModifierId)) return false;

        string craftingOrderId = null;
        if (data.CraftingOrder != null && !objectManager.TryGetIdWithLogging(data.CraftingOrder, out craftingOrderId)) return false;

        message = new NetworkCreateCraftedWeaponInternalServer(
            data.IsFreeMode,
            craftingHeroId,
            data.Name,
            cultureId,
            craftingTemplateId,
            data.WeaponDesign.WeaponName?.ToString() ?? "",
            craftingPieceIds,
            scalePercentages,
            weaponModifierId,
            playerHeroId,
            itemModifierGroupId,
            craftingOrderId,
            currentSettlementId,
            data.ClientRequestId);
        return true;
    }

    private void Handle_NetworkCreateCraftedWeaponInternalServer(MessagePayload<NetworkCreateCraftedWeaponInternalServer> obj)
    {
        var data = obj.What;
        var peer = obj.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            if (!TryCreateCraftedWeapon(data))
            {
                network.Send(peer, new NetworkCreateCraftedWeaponInternalClients(data, null, false));
            }
        });
    }

    private bool TryCreateCraftedWeapon(NetworkCreateCraftedWeaponInternalServer data)
    {
        if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior)) return false;
        if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return false;
        if (!objectManager.TryGetObjectWithLogging(data.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return false;

        ItemModifierGroup itemModifierGroup = null;
        if (data.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(data.ItemModifierGroupId, out itemModifierGroup)) return false;

        if (!GetUsedPieces(data.WeaponDesignElementCraftingPieceIds, data.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return false;

        ItemModifier weaponModifier = null;
        if (data.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(data.WeaponModifierId, out weaponModifier)) return false;

        CultureObject culture = null;
        if (data.CultureId != null && !objectManager.TryGetObjectWithLogging(data.CultureId, out culture)) return false;

        CraftingOrder craftingOrder = null;
        if (!data.IsFreeMode && (!objectManager.TryGetObjectWithLogging(data.CraftingOrderId, out craftingOrder) || !craftingOrder.IsReady)) return false;

        // Replace original TaleWorlds implementation
        string nextCraftedItemId = craftingCampaignBehavior.GetNextCraftedItemId();
        var newHeroCraftingStamina = craftingCampaignBehaviorInterface.CreateCraftedWeaponInternal(
            craftingCampaignBehavior,
            craftingHero,
            craftingTemplate,
            itemModifierGroup,
            usedPieces,
            weaponModifier,
            culture,
            data.IsFreeMode,
            data.Name,
            data.WeaponName,
            nextCraftedItemId,
            out var succeeded);

        if (!succeeded) return false;

        // Update stamina on clients
        network.SendAll(new NetworkSetHeroCraftingStamina(data.CraftingHeroId, newHeroCraftingStamina));

        // Create weapon on all clients
        NetworkCreateCraftedWeaponInternalClients message = new(data, nextCraftedItemId, true);
        network.SendAll(message);

        ApplyCraftingRewards(data, craftingHero, weaponModifier, craftingOrder, nextCraftedItemId);
        return true;
    }

    private void ApplyCraftingRewards(
        NetworkCreateCraftedWeaponInternalServer data,
        Hero craftingHero,
        ItemModifier weaponModifier,
        CraftingOrder craftingOrder,
        string craftedItemId)
    {
        if (!objectManager.TryGetObjectWithLogging<ItemObject>(craftedItemId, out var craftedItem)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;

        float gainedXp;
        if (data.IsFreeMode)
        {
            craftingCampaignBehaviorInterface.AddCraftedItemToRoster(playerHero.PartyBelongedTo.ItemRoster, weaponModifier, craftedItem);
            FlushCoalescer(playerHero.PartyBelongedTo.ItemRoster);

            gainedXp = Campaign.Current.Models.SmithingModel.GetSkillXpForSmithingInFreeBuildMode(craftedItem);
        }
        else
        {
            // Complete order on server and send result to clients
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var currentSettlement)) return;

            messageBroker.Publish(this, new CompleteOrderServer(
                currentSettlement.Town,
                craftingOrder,
                craftedItem,
                craftingHero,
                playerHero));

            gainedXp = craftingOrder.GetOrderExperience(craftedItem, weaponModifier) + Campaign.Current.Models.SmithingModel.GetSkillXpForSmithingInCraftingOrderMode(craftedItem);
        }

        craftingHero.AddSkillXp(DefaultSkills.Crafting, gainedXp);
    }

    private void Handle_NetworkCreateCraftedWeaponInternalClients(MessagePayload<NetworkCreateCraftedWeaponInternalClients> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryApplyCraftingResult(data))
            {
                messageBroker.Publish(this, new CreateCraftingResultPopup(null, false, data.ClientRequestId));
            }
        });
    }

    private bool TryApplyCraftingResult(NetworkCreateCraftedWeaponInternalClients data)
    {
        if (!data.Success) return false;

        if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return false;
        if (!objectManager.TryGetObjectWithLogging(data.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return false;
        if (!objectManager.TryGetObjectWithLogging(data.PlayerHeroId, out Hero playerHero)) return false;
        if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return false;
        if (!objectManager.TryGetObjectWithLogging(data.CurrentSettlementId, out Settlement currentSettlement)) return false;

        ItemModifierGroup itemModifierGroup = null;
        if (data.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(data.ItemModifierGroupId, out itemModifierGroup)) return false;

        if (!GetUsedPieces(data.WeaponDesignElementCraftingPieceIds, data.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return false;

        ItemModifier weaponModifier = null;
        if (data.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(data.WeaponModifierId, out weaponModifier)) return false;

        CultureObject culture = null;
        if (data.CultureId != null && !objectManager.TryGetObjectWithLogging(data.CultureId, out culture)) return false;

        ItemObject craftedItemObject;
        using (new AllowedThread())
        {
            // Replace original TaleWorlds implementation
            string nextCraftedItemId = data.NextCraftedItemId;
            WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(data.WeaponName), usedPieces);
            if (data.IsFreeMode)
            {
                weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
            }

            craftedItemObject = craftingCampaignBehaviorInterface.CreateAndRegisterCraftedItem(weaponDesign, data.Name, culture, itemModifierGroup, nextCraftedItemId);
            CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !data.IsFreeMode);

            // Only run on crafting client
            if (playerHero == Hero.MainHero)
            {
                if (GameStateManager.Current.ActiveState is CraftingState currentState)
                {
                    currentState.CraftingLogic._craftedItemObject = craftedItemObject;
                }

                AddItemToHistoryPatch.OverrideAddItemToHistory(ref craftingBehavior, craftedItemObject);
            }
        }

        if (playerHero != Hero.MainHero) return true;

        int researchPoints = Campaign.Current.Models.SmithingModel.GetPartResearchGainForSmithingItem(craftedItemObject, craftingHero, data.IsFreeMode);
        craftingBehavior.AddResearchPoints(craftedItemObject.WeaponDesign.Template, researchPoints);

        messageBroker.Publish(this, new CreateCraftingResultPopup(craftedItemObject, true, data.ClientRequestId));
        return true;
    }

    private void Handle_NetworkSetHeroCraftingStamina(MessagePayload<NetworkSetHeroCraftingStamina> obj)
    {
        var data = obj.What;
        
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return;

            craftingBehavior.GetRecordForCompanion(craftingHero).CraftingStamina = MathF.Max(0, data.Value);
        });
    }

    private bool PackUsedPieces(WeaponDesign weaponDesign, out List<string> craftingPieceIds, out List<int> scalePercentages)
    {
        craftingPieceIds = new List<string>();
        scalePercentages = new List<int>();
        foreach (var weaponDesignElement in weaponDesign._usedPieces)
        {
            if (!weaponDesignElement._craftingPiece.IsValid) // Skip invalid crafting pieces, e.g. Axe doesn't have a guard
            {
                craftingPieceIds.Add("");
                scalePercentages.Add(-1);
                continue;
            }

            if (!objectManager.TryGetIdWithLogging(weaponDesignElement._craftingPiece, out var currentCraftingPieceId)) return false;
            craftingPieceIds.Add(currentCraftingPieceId);
            scalePercentages.Add(weaponDesignElement._scalePercentage);
        }

        return true;
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

    private void FlushCoalescer(ItemRoster itemRoster)
    {
        objectManager.TryGetId(itemRoster, out var rosterId);
        var compactId = Compact(rosterId, typeof(ItemRoster));

        sendCoalescer?.FlushInstance(compactId, network);
    }
}
