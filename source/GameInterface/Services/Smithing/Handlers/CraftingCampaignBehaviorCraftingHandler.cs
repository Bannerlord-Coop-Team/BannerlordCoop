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

        messageBroker.Subscribe<DoRefinement>(Handle_DoRefinement);
        messageBroker.Subscribe<NetworkDoRefinement>(Handle_NetworkDoRefinement);

        messageBroker.Subscribe<CreatedCraftedWeaponInternal>(Handle_CreatedCraftedWeaponInternal);
        messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalServer>(Handle_NetworkCreateCraftedWeaponInternalServer);
        messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalClients>(Handle_NetworkCreateCraftedWeaponInternalClients);
        messageBroker.Subscribe<NetworkAddCraftedItemToRoster>(Handle_NetworkAddCraftedItemToRoster);

        messageBroker.Subscribe<NetworkSetHeroCraftingStamina>(Handle_NetworkSetHeroCraftingStamina);

        messageBroker.Subscribe<AddSkillXpFromCrafting>(Handle_AddSkillXpFromCrafting);
        messageBroker.Subscribe<NetworkAddSkillXpFromCrafting>(Handle_NetworkAddSkillXpFromCrafting);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DoSmelting>(Handle_DoSmelting);
        messageBroker.Unsubscribe<NetworkDoSmelting>(Handle_NetworkDoSmelting);

        messageBroker.Unsubscribe<DoRefinement>(Handle_DoRefinement);
        messageBroker.Unsubscribe<NetworkDoRefinement>(Handle_NetworkDoRefinement);

        messageBroker.Unsubscribe<CreatedCraftedWeaponInternal>(Handle_CreatedCraftedWeaponInternal);
        messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalServer>(Handle_NetworkCreateCraftedWeaponInternalServer);
        messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalClients>(Handle_NetworkCreateCraftedWeaponInternalClients);
        messageBroker.Unsubscribe<NetworkAddCraftedItemToRoster>(Handle_NetworkAddCraftedItemToRoster);

        messageBroker.Unsubscribe<NetworkSetHeroCraftingStamina>(Handle_NetworkSetHeroCraftingStamina);

        messageBroker.Unsubscribe<AddSkillXpFromCrafting>(Handle_AddSkillXpFromCrafting);
        messageBroker.Unsubscribe<NetworkAddSkillXpFromCrafting>(Handle_NetworkAddSkillXpFromCrafting);
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
            craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior);
            if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return;

            // Replace original TaleWorlds implementation
            var newHeroCraftingStamina = craftingCampaignBehaviorInterface.DoSmelting(craftingCampaignBehavior, craftingHero, data.EquipmentElement);

            // Update stamina on clients
            network.SendAll(new NetworkSetHeroCraftingStamina(data.CraftingHeroId, newHeroCraftingStamina));

            // Refresh client view model
            FlushCoalescer(craftingHero.PartyBelongedTo.ItemRoster);
            network.Send(obj.Who as NetPeer, new NetworkRefreshSmelting());
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
            craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior);
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

        if (!objectManager.TryGetIdWithLogging(data.CraftingHero, out var craftingHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.WeaponDesign.Template, out var craftingTemplateId)) return;
        if (!objectManager.TryGetIdWithLogging(data.PlayerHero, out var playerHeroId)) return;

        string itemModifierGroupId = null;
        if (data.CraftingLogic.CurrentItemModifierGroup != null && !objectManager.TryGetIdWithLogging(data.CraftingLogic.CurrentItemModifierGroup, out itemModifierGroupId)) return;

        string cultureId = null;
        if (data.CultureObject != null && !objectManager.TryGetIdWithLogging(data.CultureObject, out cultureId)) return;

        if (!PackUsedPieces(data.WeaponDesign, out var craftingPieceIds, out var scalePercentages)) return;

        var weaponModifierId = "";
        if (data.WeaponModifier != null && !objectManager.TryGetIdWithLogging(data.WeaponModifier, out weaponModifierId)) return;

        string craftingOrderId = null;
        if (data.CraftingOrder != null && !objectManager.TryGetIdWithLogging(data.CraftingOrder, out craftingOrderId)) return;

        NetworkCreateCraftedWeaponInternalServer message = new(
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
            craftingOrderId
        );
        network.SendAll(message);
    }

    private void Handle_NetworkCreateCraftedWeaponInternalServer(MessagePayload<NetworkCreateCraftedWeaponInternalServer> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingCampaignBehavior);
            if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return;
            if (!objectManager.TryGetObjectWithLogging(data.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return;

            ItemModifierGroup itemModifierGroup = null;
            if (data.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(data.ItemModifierGroupId, out itemModifierGroup)) return;

            if (!GetUsedPieces(data.WeaponDesignElementCraftingPieceIds, data.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return;

            ItemModifier weaponModifier = null;
            if (data.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(data.WeaponModifierId, out weaponModifier)) return;

            CultureObject culture = null;
            if (data.CultureId != null && !objectManager.TryGetObjectWithLogging(data.CultureId, out culture)) return;

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
                nextCraftedItemId);

            // Update stamina on clients
            network.SendAll(new NetworkSetHeroCraftingStamina(data.CraftingHeroId, newHeroCraftingStamina));

            // Create weapon on all clients
            NetworkCreateCraftedWeaponInternalClients message = new(data, nextCraftedItemId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCreateCraftedWeaponInternalClients(MessagePayload<NetworkCreateCraftedWeaponInternalClients> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;
                if (!objectManager.TryGetObjectWithLogging(data.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return;
                if (!objectManager.TryGetObjectWithLogging(data.PlayerHeroId, out Hero playerHero)) return;
                if (!objectManager.TryGetObjectWithLogging(data.CraftingHeroId, out Hero craftingHero)) return;

                ItemModifierGroup itemModifierGroup = null;
                if (data.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(data.ItemModifierGroupId, out itemModifierGroup)) return;

                if (!GetUsedPieces(data.WeaponDesignElementCraftingPieceIds, data.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return;

                ItemModifier weaponModifier = null;
                if (data.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(data.WeaponModifierId, out weaponModifier)) return;

                CultureObject culture = null;
                if (data.CultureId != null && !objectManager.TryGetObjectWithLogging(data.CultureId, out culture)) return;

                CraftingOrder craftingOrder = null;
                if (data.CraftingOrderId != null && !objectManager.TryGetObjectWithLogging(data.CraftingOrderId, out craftingOrder)) return;

                // Replace original TaleWorlds implementation
                string nextCraftedItemId = data.NextCraftedItemId;
                WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(data.WeaponName), usedPieces);
                if (data.IsFreeMode)
                {
                    weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
                }

                var craftedItemObject = craftingCampaignBehaviorInterface.CreateAndRegisterCraftedItem(weaponDesign, data.Name, culture, itemModifierGroup, nextCraftedItemId);
                CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !data.IsFreeMode);

                // Only run on crafting client
                if (playerHero == Hero.MainHero)
                {
                    if (GameStateManager.Current.ActiveState is CraftingState currentState)
                    {
                        currentState.CraftingLogic._craftedItemObject = craftedItemObject;
                    }

                    // Update client's WeaponDesignVM to be referencing this crafted item instead
                    messageBroker.Publish(this, new UpdateCraftedItem(craftedItemObject));

                    AddItemToHistoryPatch.OverrideAddItemToHistory(ref craftingBehavior, craftedItemObject);

                    if (!objectManager.TryGetIdWithLogging(craftedItemObject, out var craftedItemId)) return;

                    // Add to item rosters after the item has finished being created on clients
                    // Won't resolve on clients when running AddToCounts otherwise
                    if (data.IsFreeMode)
                    {
                        var message = new NetworkAddCraftedItemToRoster(craftedItemId, data.PlayerHeroId, data.WeaponModifierId);
                        network.SendAll(message);
                    }
                    else // Complete order
                    {
                        messageBroker.Publish(this, new CompleteOrderFromVM(craftingOrder, craftedItemObject, craftingHero));
                    }
                }
            }
        });
    }

    private void Handle_NetworkAddCraftedItemToRoster(MessagePayload<NetworkAddCraftedItemToRoster> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(data.CraftedItemId, out ItemObject craftedItemObject)) return;
            if (!objectManager.TryGetObjectWithLogging(data.PlayerHeroId, out Hero playerHero)) return;

            ItemModifier weaponModifier = null;
            if (data.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(data.WeaponModifierId, out weaponModifier)) return;

            craftingCampaignBehaviorInterface.AddCraftedItemToRoster(playerHero.PartyBelongedTo.ItemRoster, weaponModifier, craftedItemObject);
        });
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

    private void Handle_AddSkillXpFromCrafting(MessagePayload<AddSkillXpFromCrafting> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingHero, out var craftingHeroId)) return;

        network.SendAll(new NetworkAddSkillXpFromCrafting(craftingHeroId, obj.What.Xp));
    }

    private void Handle_NetworkAddSkillXpFromCrafting(MessagePayload<NetworkAddSkillXpFromCrafting> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.CraftingHeroId, out var craftingHero)) return;

            craftingHero.AddSkillXp(DefaultSkills.Crafting, obj.What.Xp);
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
