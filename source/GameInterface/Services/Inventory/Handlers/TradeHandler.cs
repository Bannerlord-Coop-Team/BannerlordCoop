using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Inventory.Handlers;

internal class TradeHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<TradeHandler>();

    private readonly IInventoryLogicInterface inventoryLogicInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public TradeHandler(
        IInventoryLogicInterface inventoryLogicInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.inventoryLogicInterface = inventoryLogicInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<TradeAttempted>(Handle_TradeAttempted);
        messageBroker.Subscribe<CompleteTrade>(Handle_CompleteTrade);
        messageBroker.Subscribe<UpdateEquipmentClients>(Handle_UpdateEquipmentClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TradeAttempted>(Handle_TradeAttempted);
        messageBroker.Unsubscribe<CompleteTrade>(Handle_CompleteTrade);
        messageBroker.Unsubscribe<UpdateEquipmentClients>(Handle_UpdateEquipmentClients);
    }

    private void Handle_TradeAttempted(MessagePayload<TradeAttempted> payload)
    {
        var what = payload.What;

        string fromRosterId = null;
        if (!what.CanGainXpFromDiscarding && !objectManager.TryGetIdWithLogging(what.FromRoster, out fromRosterId)) return;

        if (!objectManager.TryGetIdWithLogging(what.ToRoster, out var toRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(what.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(what.TroopRoster, out var troopRosterId)) return;
        string mobilePartyId = null;
        if (what.Party is not null && !objectManager.TryGetIdWithLogging(what.Party, out mobilePartyId)) return;

        string currentSettlementComponentId = null;
        if (what.CurrentSettlementComponent is not null && 
            !objectManager.TryGetIdWithLogging(what.CurrentSettlementComponent, out currentSettlementComponentId)) return;

        var boughtItems = ResolveTradeItemIds(what.BoughtItems);
        var soldItems = ResolveTradeItemIds(what.SoldItems);

        var characterIdEquipmentsData = ResolveCharacterIdEquipmentsData(what.Party);

        var troopRosterData = troopRosterInterface.PackTroopRosterData(what.TroopRoster);

        var message = new CompleteTrade(
            fromRosterId,
            fromRosterId is null,
            toRosterId,
            what.FromRoster._data,
            what.ToRoster._data,
            characterIdEquipmentsData,
            what.IsTrading,
            what.CanGainXpFromDiscarding,
            heroId,
            what.TotalAmount,
            what.MerchantGold,
            mobilePartyId,
            currentSettlementComponentId is null,
            currentSettlementComponentId,
            boughtItems,
            soldItems,
            troopRosterId,
            troopRosterData
        );

        network.SendAll(message);
    }

    private void Handle_CompleteTrade(MessagePayload<CompleteTrade> payload)
    {
        var message = payload.What;

        ItemRoster fromRoster = null;
        if (!message.IsFromItemRosterNull && !objectManager.TryGetObjectWithLogging<ItemRoster>(message.FromItemRosterId, out fromRoster)) return;

        if (!objectManager.TryGetObjectWithLogging<ItemRoster>(message.ToItemRosterId, out var toRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(message.HeroId, out var hero)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(message.PartyId, out var mobileParty)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(message.TroopRosterId, out var troopRoster)) return;

        SettlementComponent currentSettlementComponent = null;
        if (!message.IsSettlementComponentNull && 
            !objectManager.TryGetObjectWithLogging<SettlementComponent>(message.CurrentSettlementComponentId, out currentSettlementComponent)) return;

        var boughtItems = ResolveTradeItems(message.BoughtItems);
        var soldItems = ResolveTradeItems(message.SoldItems);
        ResolveCharacterEquipmentsData(message.CharacterIdEquipmentsData, out var characterEquipmentsData);

        var fromItemRosterData = message.FromItemRosterData;
        var toItemRosterData = message.ToItemRosterData;
        var totalAmount = message.TotalAmount;

        // Undo any purchases that are no longer present in the roster
        // When looting, taken items are treated as bought items. Don't need to manage changed rosters in these cases
        if (fromRoster != null)
        {
            foreach (var boughtItem in boughtItems)
            {
                int difference = fromRoster.GetItemNumber(boughtItem.Item1.EquipmentElement.Item) - boughtItem.Item1.Amount;

                if (difference < 0)
                {
                    int fromRosterDataIndex = fromItemRosterData.FindIndex(rosterElement => rosterElement.EquipmentElement.Equals(boughtItem.Item1));
                    if (fromRosterDataIndex >= 0) fromItemRosterData[fromRosterDataIndex].Amount -= difference;
                    else fromItemRosterData.AddItem(new ItemRosterElement(boughtItem.Item1.EquipmentElement, -difference));

                    int toRosterDataIndex = toItemRosterData.FindIndex(rosterElement => rosterElement.EquipmentElement.Equals(boughtItem.Item1));
                    if (toRosterDataIndex >= 0) toItemRosterData[toRosterDataIndex].Amount += difference;
                    else toItemRosterData.AddItem(new ItemRosterElement(boughtItem.Item1.EquipmentElement, difference));

                    totalAmount -= boughtItem.Item2;
                }
            }
        }

        // Update rosters with new data
        if (fromRoster != null) inventoryLogicInterface.UpdateRosterWithData(fromRoster, fromItemRosterData);
        if (toRoster != null) inventoryLogicInterface.UpdateRosterWithData(toRoster, toItemRosterData);

        // Update hero equipment with new data
        inventoryLogicInterface.UpdateEquipmentWithData(mobileParty, characterEquipmentsData);
        network.SendAll(new UpdateEquipmentClients(message.CharacterIdEquipmentsData, message.PartyId));

        // Update troop roster for if items were donated
        troopRosterInterface.UpdateWithData(troopRoster, message.TroopRosterData, hero);

        inventoryLogicInterface.ApplyDoneLogic(
            fromRoster,
            toRoster,
            message.IsTrading,
            message.CanGainXpFromDiscarding,
            hero,
            totalAmount,
            message.MerchantGold,
            mobileParty,
            currentSettlementComponent,
            boughtItems,
            soldItems
        );

        if (hero.CharacterObject != null && hero != null && message.IsTrading)
        {
            network.Send(payload.Who as NetPeer, new NotifyGoldChange(-totalAmount));
        }
    }

    private void Handle_UpdateEquipmentClients(MessagePayload<UpdateEquipmentClients> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

        ResolveCharacterEquipmentsData(obj.What.CharacterIdEquipmentsData, out var characterEquipmentsData);
        inventoryLogicInterface.UpdateEquipmentWithData(mobileParty, characterEquipmentsData);
    }

    private (ItemRosterElementData, int)[] ResolveTradeItemIds(
        IEnumerable<(ItemRosterElement, int)> items)
    {
        var resolvedItems = new List<(ItemRosterElementData, int)>();

        foreach (var (item, count) in items)
        {
            if (TryResolveItemRosterId(item, out var resolvedItem))
            {
                resolvedItems.Add((resolvedItem, count));
            }
        }

        return resolvedItems.ToArray();
    }

    private List<(ItemRosterElement, int)> ResolveTradeItems(
        IEnumerable<(ItemRosterElementData, int)> items)
    {
        var resolvedItems = new List<(ItemRosterElement, int)>();

        if (items == null)
            return resolvedItems;

        foreach (var (itemData, count) in items)
        {
            if (TryResolveItemRosterElement(itemData, out var item))
            {
                resolvedItems.Add((item, count));
            }
        }

        return resolvedItems;
    }

    private bool TryResolveItemRosterElement(ItemRosterElementData data, out ItemRosterElement result)
    {
        result = default;

        var itemObjectData = data.ItemObjectData;

        if (!objectManager.TryGetObject<ItemObject>(itemObjectData.ItemObjectId, out var itemObject))
        {
            logger.Error("Failed to get {type} with id: {id}", typeof(ItemObject), itemObjectData.ItemObjectId);
            return false;
        }

        ItemModifier itemModifier = null;
        if (!itemObjectData.ItemModifierNull && !objectManager.TryGetObject(itemObjectData.ItemModifierId, out itemModifier))
        {
            logger.Error("Failed to get {type} with id: {id}", typeof(ItemModifier), itemObjectData.ItemModifierId);
            return false;
        }

        using (new AllowedThread())
        {
            result = new ItemRosterElement(itemObject, data.Amount, itemModifier);
        }

        return true;
    }

    private bool TryResolveItemRosterId(ItemRosterElement itemRosterElement, out ItemRosterElementData result)
    {
        result = default;

        if (!objectManager.TryGetId(itemRosterElement.EquipmentElement.Item, out var itemObjectId))
        {
            logger.Error("Failed to get id for {type}", nameof(itemRosterElement.EquipmentElement.Item));
            return false;
        }

        string itemModifierId = null;
        if (itemRosterElement.EquipmentElement.ItemModifier is not null && !objectManager.TryGetId(itemRosterElement.EquipmentElement.ItemModifier, out itemModifierId))
        {
            logger.Error("Failed to get id for {type}", nameof(itemRosterElement.EquipmentElement.ItemModifier));
            return false;
        }

        var itemModifierNull = itemRosterElement.EquipmentElement.ItemModifier is null;

        result = new ItemRosterElementData(
            new ItemObjectData(itemObjectId, itemModifierId, itemModifierNull),
            itemRosterElement.Amount
        );

        return true;
    }

    private Dictionary<string, EquipmentData[]> ResolveCharacterIdEquipmentsData(MobileParty party)
    {
        var characterIdEquipmentsData = new Dictionary<string, EquipmentData[]>();
        for (int i = 0; i < party.MemberRoster.Count; i++)
        {
            CharacterObject character = party.MemberRoster.GetElementCopyAtIndex(i).Character;
            if (character.IsHero)
            {
                if (!objectManager.TryGetIdWithLogging(character.HeroObject, out var heroId)) continue;

                characterIdEquipmentsData.Add(heroId, new EquipmentData[]
                {
                    new EquipmentData(character.FirstBattleEquipment._equipmentType, character.FirstBattleEquipment._itemSlots),
                    new EquipmentData(character.FirstCivilianEquipment._equipmentType, character.FirstCivilianEquipment._itemSlots),
                    new EquipmentData(character.FirstStealthEquipment._equipmentType, character.FirstStealthEquipment._itemSlots)
                });
            }
        }
        return characterIdEquipmentsData;
    }

    private void ResolveCharacterEquipmentsData(Dictionary<string, EquipmentData[]> characterIdEquipmentsData, out Dictionary<CharacterObject, Equipment[]> characterEquipmentsData)
    {
        characterEquipmentsData = new();
        foreach (KeyValuePair<string, EquipmentData[]> characterIdEquipment in characterIdEquipmentsData)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(characterIdEquipment.Key, out var hero)) continue;

            var character = hero.CharacterObject;
            characterEquipmentsData[character] = new Equipment[3];
            for (int i = 0; i < 3; i++)
            {
                characterEquipmentsData[character][i] = ResolveEquipmentData(characterIdEquipment.Value[i]);
            }
        }
    }
    
    private Equipment ResolveEquipmentData(EquipmentData equipmentData)
    {
        Equipment equipment = new(equipmentData.EquipmentType);
        for (int i = 0; i < Equipment.EquipmentSlotLength; i++)
        {
            equipment._itemSlots[i] = equipmentData.ItemSlots[i];
        }
        return equipment;
    }
}
