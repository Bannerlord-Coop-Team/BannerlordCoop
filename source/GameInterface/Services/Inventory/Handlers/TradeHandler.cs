using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using Helpers;
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
    private readonly IVillageHostileActionInterface villageHostileActionInterface;

    public TradeHandler(
        IInventoryLogicInterface inventoryLogicInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IVillageHostileActionInterface villageHostileActionInterface)
    {
        this.inventoryLogicInterface = inventoryLogicInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.villageHostileActionInterface = villageHostileActionInterface;

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

        var isManagingWarehouse = what.InventoryMode == InventoryScreenHelper.InventoryMode.Warehouse;

        // Don't update warehouse rosters directly if managing a warehouse, server uses CoopSession.WorkshopPlayerData
        // Not all left rosters need to be managed by server so no need to check result of resolving it
        // e.g. Discarding items in default inventory screen only needs to save the right roster
        // Don't need to log the check here, from roster can not resolve legimately
        string fromRosterId = null;
        if (!isManagingWarehouse) objectManager.TryGetId(what.FromRoster, out fromRosterId);

        if (!objectManager.TryGetIdWithLogging(what.ToRoster, out var toRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(what.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(what.OwnerParty, out var ownerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(what.InitialCharacterEquipment.HeroObject, out var initialHeroId)) return;

        // CurrentMobileParty can be already destroyed when this logic runs. Attempt to get an id without logging
        objectManager.TryGetId(what.CurrentMobileParty, out var currentMobilePartyId);

        string currentSettlementComponentId = null;
        if (what.CurrentSettlementComponent is not null && 
            !objectManager.TryGetIdWithLogging(what.CurrentSettlementComponent, out currentSettlementComponentId)) return;

        var boughtItems = ResolveTradeItemIds(what.BoughtItems);
        var soldItems = ResolveTradeItemIds(what.SoldItems);

        if (what.CanGainXpFromDiscarding)
        {
            soldItems = ResolveLeftLootIds(what.FromRoster._data);
        }

        var characterIdEquipmentsData = ResolveCharacterIdEquipmentsData(what.OwnerParty, what.InitialCharacterEquipment);

        var message = new CompleteTrade(
            fromRosterId,
            fromRosterId is null,
            toRosterId,
            what.FromRoster._data,
            what.ToRoster._data,
            characterIdEquipmentsData,
            what.IsTrading,
            what.CanGainXpFromDiscarding,
            isManagingWarehouse,
            heroId,
            initialHeroId,
            what.TotalAmount,
            what.MerchantGold,
            ownerPartyId,
            currentMobilePartyId,
            currentSettlementComponentId is null,
            currentSettlementComponentId,
            boughtItems,
            soldItems,
            what.ForceTransferId
        );

        network.SendAll(message);
    }

    private void Handle_CompleteTrade(MessagePayload<CompleteTrade> payload)
    {
        var message = payload.What;
        var peer = payload.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            ItemRoster fromRoster = null;
            if (!message.IsFromItemRosterNull && !objectManager.TryGetObjectWithLogging<ItemRoster>(message.FromItemRosterId, out fromRoster)) return;

            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(message.ToItemRosterId, out var toRoster)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(message.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(message.OwnerPartyId, out var ownerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(message.InitialHeroId, out var initialHero)) return;

            SettlementComponent currentSettlementComponent = null;
            if (!message.IsSettlementComponentNull &&
                !objectManager.TryGetObjectWithLogging<SettlementComponent>(message.CurrentSettlementComponentId, out currentSettlementComponent)) return;

            MobileParty currentMobileParty = null;
            if (message.CurrentMobilePartyId != null && !objectManager.TryGetObjectWithLogging<MobileParty>(message.CurrentMobilePartyId, out currentMobileParty)) return;

            var boughtItems = ResolveTradeItems(message.BoughtItems);
            var soldItems = ResolveTradeItems(message.SoldItems);
            ResolveCharacterEquipmentsData(message.CharacterIdEquipmentsData, out var characterEquipmentsData);

            if (ModInformation.IsServer && !message.IsTrading)
            {
                if (message.ForceTransferId != null)
                {
                    if (!TryValidateForceTransfer(message, peer))
                        return;
                }
                else if (villageHostileActionInterface.HasPendingForceTransferForParty(message.OwnerPartyId))
                {
                    // Attribution missed (see ForceTransferScreenTracker): the cap cannot
                    // be enforced on this commit. Logged so live runs prove attribution
                    // works; still applied under the trusted-client model.
                    logger.Warning(
                        "ForceTransfer loot commit without attribution while a pool is pending (Party={PartyId})",
                        message.OwnerPartyId);
                }
            }

            var fromItemRosterData = message.FromItemRosterData;
            var toItemRosterData = message.ToItemRosterData;
            var totalAmount = message.TotalAmount;

            // Undo any purchases that are no longer present in the roster
            // When looting, taken items are treated as bought items. Don't need to manage changed rosters in these cases
            if (fromRoster != null)
            {
                totalAmount = ReconcilePurchases(fromRoster, fromItemRosterData, toItemRosterData, boughtItems, totalAmount);
            }

            // Update rosters with new data
            if (toRoster != null) inventoryLogicInterface.UpdateRosterWithData(toRoster, toItemRosterData);
            if (fromRoster != null) inventoryLogicInterface.UpdateRosterWithData(fromRoster, fromItemRosterData);
            else if (message.IsManagingWarehouse)
            {
                // Manage warehouse rosters separately as they involve more complicated logic
                MessageBroker.Instance.Publish(this, new WarehouseRosterManaged(payload.Who as NetPeer, hero, currentSettlementComponent.Settlement, fromItemRosterData));
            }

            // Update hero equipment with new data
            inventoryLogicInterface.UpdateEquipmentWithData(ownerParty, characterEquipmentsData, initialHero);
            network.SendAll(new UpdateEquipmentClients(message.CharacterIdEquipmentsData, message.OwnerPartyId, message.InitialHeroId));

            inventoryLogicInterface.ApplyDoneLogic(
                fromRoster,
                toRoster,
                message.IsTrading,
                message.CanGainXpFromDiscarding,
                hero,
                totalAmount,
                message.MerchantGold,
                currentMobileParty,
                currentSettlementComponent,
                boughtItems,
                soldItems
            );
        });
    }

    private bool TryValidateForceTransfer(CompleteTrade message, NetPeer peer)
    {
        if (!TryResolveLeftRemainder(message.FromItemRosterData, out var leftRemainder, out var remainderError))
        {
            logger.Warning(
                "Rejected force supplies transfer with unverifiable left roster (Party={PartyId}, Request={RequestId}, Reason={Reason})",
                message.OwnerPartyId,
                message.ForceTransferId,
                remainderError);
        }
        else if (!villageHostileActionInterface.TryPeekForceTransfer(message.ForceTransferId, message.OwnerPartyId, out var pool))
        {
            logger.Warning(
                "Rejected force supplies transfer with stale or consumed pool (Party={PartyId}, Request={RequestId})",
                message.OwnerPartyId,
                message.ForceTransferId);
        }
        else if (!VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(pool.SuppliesItems, message.BoughtItems, leftRemainder, out var error))
        {
            // The pool is deliberately left intact: a rejection must not mutate
            // pool state, so a false positive never destroys the reward.
            logger.Warning(
                "Rejected force supplies transfer exceeding the authorized pool (Party={PartyId}, Request={RequestId}, Reason={Reason})",
                message.OwnerPartyId,
                message.ForceTransferId,
                error);
        }
        else if (!villageHostileActionInterface.TryConsumeForceTransfer(message.ForceTransferId, message.OwnerPartyId, out _))
        {
            logger.Warning(
                "Rejected force supplies transfer with stale or consumed pool (Party={PartyId}, Request={RequestId})",
                message.OwnerPartyId,
                message.ForceTransferId);
        }
        else
        {
            logger.Information(
                "ForceTransfer supplies commit accepted (Party={PartyId}, Request={RequestId})",
                message.OwnerPartyId,
                message.ForceTransferId);
            return true;
        }

        if (peer != null)
            network.Send(peer, new SendInformationMessage("The village has nothing left to give."));
        return false;
    }

    internal bool TryResolveLeftRemainder(ItemRosterElement[] leftRosterData, out List<(string itemId, string modifierId, int amount)> remainder, out string error)
    {
        remainder = new List<(string itemId, string modifierId, int amount)>();
        error = null;
        if (leftRosterData == null)
            return true;

        for (var i = 0; i < leftRosterData.Length; i++)
        {
            var element = leftRosterData[i];
            var item = element.EquipmentElement.Item;
            if (item == null && element.Amount == 0)
            {
                // Vanilla leaves empty tombstone slots behind when a kind is fully
                // taken. They carry no outflow, so they are skipped, not rejected.
                // A null item with any other amount is corrupt and rejected below.
                continue;
            }
            if (!objectManager.TryGetId(item, out var itemId))
            {
                // Either a corrupt slot (null item, positive amount) or a real item
                // the server cannot resolve.
                error = $"left remainder element [{i}/{leftRosterData.Length}] without registry id " +
                    $"(Amount={element.Amount}, ItemNull={item == null}, StringId={item?.StringId ?? "<null>"})";
                return false;
            }

            string modifierId = null;
            var modifier = element.EquipmentElement.ItemModifier;
            if (modifier != null &&
                !objectManager.TryGetId(modifier, out modifierId))
            {
                error = $"left remainder element [{i}/{leftRosterData.Length}] modifier without registry id " +
                    $"(Amount={element.Amount}, StringId={modifier?.StringId ?? "<null>"})";
                return false;
            }

            remainder.Add((itemId, modifierId, element.Amount));
        }

        return true;
    }

    private void Handle_UpdateEquipmentClients(MessagePayload<UpdateEquipmentClients> obj)    {
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
                if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.InitialHeroId, out var initialHero)) return;

                ResolveCharacterEquipmentsData(obj.What.CharacterIdEquipmentsData, out var characterEquipmentsData);
                inventoryLogicInterface.UpdateEquipmentWithData(mobileParty, characterEquipmentsData, initialHero);
            }
        });
    }

    /// <summary>
    /// Removes any purchased quantity the merchant can no longer supply from the party roster data and
    /// refunds the prorated cost.
    /// </summary>
    internal static int ReconcilePurchases(
        ItemRoster merchantRoster,
        ItemRosterElement[] merchantRosterData,
        ItemRosterElement[] partyRosterData,
        List<(ItemRosterElement, int)> boughtItems,
        int totalAmount)
    {
        foreach (var boughtItem in boughtItems)
        {
            int amount = boughtItem.Item1.Amount;
            int elementIndex = merchantRoster.FindIndexOfElement(boughtItem.Item1.EquipmentElement);
            int available = elementIndex >= 0 ? merchantRoster.GetElementNumber(elementIndex) : 0;
            int difference = available - amount;

            if (difference < 0)
            {
                int fromRosterDataIndex = merchantRosterData.FindIndex(rosterElement => rosterElement.EquipmentElement.Equals(boughtItem.Item1));
                if (fromRosterDataIndex >= 0) merchantRosterData[fromRosterDataIndex].Amount -= difference;
                else merchantRosterData.AddItem(new ItemRosterElement(boughtItem.Item1.EquipmentElement, -difference));

                int toRosterDataIndex = partyRosterData.FindIndex(rosterElement => rosterElement.EquipmentElement.Equals(boughtItem.Item1));
                if (toRosterDataIndex >= 0) partyRosterData[toRosterDataIndex].Amount += difference;
                else partyRosterData.AddItem(new ItemRosterElement(boughtItem.Item1.EquipmentElement, difference));

                totalAmount -= amount > 0 ? (boughtItem.Item2 * -difference) / amount : boughtItem.Item2;
            }
        }

        return totalAmount;
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

    private (ItemRosterElementData, int)[] ResolveLeftLootIds(ItemRosterElement[] items)
    {
        var resolvedItems = new List<(ItemRosterElementData, int)>();

        for (int i = 0; i < items.Length; i++)
        {
            if (TryResolveItemRosterId(items[i], out var resolvedItem))
            {
                resolvedItems.Add((resolvedItem, items[i].Amount));
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

    private Dictionary<string, EquipmentData[]> ResolveCharacterIdEquipmentsData(MobileParty party, CharacterObject initialCharacter)
    {
        var characterIdEquipmentsData = new Dictionary<string, EquipmentData[]>();
        bool initialCharacterInParty = false;

        for (int i = 0; i < party.MemberRoster.Count; i++)
        {
            var character = party.MemberRoster.GetElementCopyAtIndex(i).Character;

            AddHeroEquipmentData(characterIdEquipmentsData, character);

            if (character == initialCharacter)
            {
                initialCharacterInParty = true;
            }
        }

        if (!initialCharacterInParty)
        {
            AddHeroEquipmentData(characterIdEquipmentsData, initialCharacter);
        }

        return characterIdEquipmentsData;
    }

    private void AddHeroEquipmentData(Dictionary<string, EquipmentData[]> characterIdEquipmentsData, CharacterObject character)
    {
        if (!character.IsHero) return;

        if (!objectManager.TryGetIdWithLogging(character.HeroObject, out var heroId)) return;

        characterIdEquipmentsData[heroId] = new EquipmentData[]
        {
            new EquipmentData(character.FirstBattleEquipment._equipmentType, character.FirstBattleEquipment._itemSlots),
            new EquipmentData(character.FirstCivilianEquipment._equipmentType, character.FirstCivilianEquipment._itemSlots),
            new EquipmentData(character.FirstStealthEquipment._equipmentType, character.FirstStealthEquipment._itemSlots)
        };
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
