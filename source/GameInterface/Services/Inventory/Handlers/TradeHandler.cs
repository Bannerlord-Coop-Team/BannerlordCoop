using Common;
using System;
using GameInterface.Services.ItemRosters.Messages;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
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
using System.Linq;
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
    private readonly ISendCoalescer sendCoalescer;
    private bool warnedMissingCoalescer;

    public TradeHandler(
        IInventoryLogicInterface inventoryLogicInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IVillageHostileActionInterface villageHostileActionInterface,
        ISendCoalescer sendCoalescer = null)
    {
        this.inventoryLogicInterface = inventoryLogicInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<TradeAttempted>(Handle_TradeAttempted);
        messageBroker.Subscribe<CompleteTrade>(Handle_CompleteTrade);
        messageBroker.Subscribe<UpdateEquipmentClients>(Handle_UpdateEquipmentClients);
        messageBroker.Subscribe<NetworkTradeCompleted>(Handle_TradeCompleted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TradeAttempted>(Handle_TradeAttempted);
        messageBroker.Unsubscribe<CompleteTrade>(Handle_CompleteTrade);
        messageBroker.Unsubscribe<UpdateEquipmentClients>(Handle_UpdateEquipmentClients);
        messageBroker.Unsubscribe<NetworkTradeCompleted>(Handle_TradeCompleted);
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
        (ItemRosterElementData, int)[] transactionSoldItems = null;

        if (what.CanGainXpFromDiscarding)
        {
            transactionSoldItems = soldItems;
            soldItems = ResolveLeftLootIds(what.FromRoster);
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
            what.ForceTransferId,
            what.InitialPlayerRoster,
            transactionSoldItems
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

            var toItemRosterData = RebaseInventoryData(
                toRoster, message.InitialPlayerRoster, message.ToItemRosterData);

            var boughtItems = ResolveTradeItems(message.BoughtItems);
            var soldItems = ResolveTradeItems(message.SoldItems);

            if (ModInformation.IsServer && !message.IsTrading)
            {
                if (message.ForceTransferId != null)
                {
                    if (!TryValidateForceTransfer(message, peer))
                    {
                        logger.Debug("Force transfer trade rejected, no inventory exchange event sent (Party={PartyId})", message.OwnerPartyId);
                        return;
                    }
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
            var totalAmount = message.TotalAmount;
            var merchantGold = message.MerchantGold;
            var purchasedItems = boughtItems;

            // Undo any purchases that are no longer present in the roster
            // When looting, taken items are treated as bought items
            if (fromRoster != null)
            {
                var heroEquipments = GetHeroTradeEquipments(message.CharacterIdEquipmentsData);
                totalAmount = ReconcilePurchases(fromRoster, toItemRosterData, heroEquipments, boughtItems, totalAmount, out var keptItems);
                purchasedItems = GetKeptPurchases(boughtItems, keptItems);

                // Merchants are shared, the client's snapshot misses what other players traded since it was taken
                if (message.IsTrading) fromItemRosterData = ApplyTradeToLiveStock(fromRoster, keptItems, soldItems);
            }

            if (message.IsTrading) merchantGold = GetLiveMerchantGold(currentSettlementComponent, currentMobileParty, merchantGold);

            ResolveCharacterEquipmentsData(message.CharacterIdEquipmentsData, out var characterEquipmentsData);

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
                merchantGold,
                currentMobileParty,
                currentSettlementComponent,
                boughtItems,
                soldItems
            );

            var exchangedSoldItems = message.CanGainXpFromDiscarding ? ResolveTradeItems(message.TransactionSoldItems) : soldItems;
            SendTradeCompleted(peer, toRoster, hero, purchasedItems, exchangedSoldItems, message.IsTrading);
        });
    }

    /// <summary>
    /// Vanilla's done logic, which Coop's patch skips, raises the player inventory exchange event on the player's game.
    /// The trader's roster and gold updates are sent first, so listeners that recount the roster see the trade.
    /// </summary>
    private void SendTradeCompleted(
        NetPeer peer,
        ItemRoster toRoster,
        Hero hero,
        List<(ItemRosterElement, int)> purchasedItems,
        List<(ItemRosterElement, int)> soldItems,
        bool isTrading)
    {
        if (peer == null)
        {
            logger.Warning("Trade applied without a sending peer, no inventory exchange event sent");
            return;
        }

        if (sendCoalescer == null)
        {
            if (!warnedMissingCoalescer)
                logger.Warning("No send coalescer, the inventory exchange event may reach the trader before the roster update");
            warnedMissingCoalescer = true;
        }
        else
        {
            if (objectManager.TryGetHandle(toRoster, out var rosterHandle)) sendCoalescer.FlushInstance(rosterHandle, network);
            else logger.Debug("Trader roster has no network handle, its update was not flushed before the inventory exchange event");

            if (objectManager.TryGetHandle(hero, out var heroHandle)) sendCoalescer.FlushInstance(heroHandle, network);
            else logger.Debug("Trader hero has no network handle, its gold was not flushed before the inventory exchange event");
        }

        network.Send(peer, new NetworkTradeCompleted(
            ResolveTradeItemIds(purchasedItems),
            ResolveTradeItemIds(soldItems),
            isTrading));
    }

    private void Handle_TradeCompleted(MessagePayload<NetworkTradeCompleted> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            var purchasedItems = ResolveTradeItems(message.PurchasedItems);
            var soldItems = ResolveTradeItems(message.SoldItems);

            CampaignEventDispatcher.Instance.OnPlayerInventoryExchange(purchasedItems, soldItems, message.IsTrading);
        });
    }

    // Each purchase at the quantity the merchant could still supply, with its share of the price
    private static List<(ItemRosterElement, int)> GetKeptPurchases(
        List<(ItemRosterElement, int)> boughtItems,
        List<ItemRosterElement> keptItems)
    {
        var purchases = new List<(ItemRosterElement, int)>();

        for (int i = 0; i < keptItems.Count; i++)
        {
            var (bought, price) = boughtItems[i];
            var kept = keptItems[i];
            if (kept.Amount <= 0) continue;

            purchases.Add((kept, price - GetRefund(price, bought.Amount - kept.Amount, bought.Amount)));
        }

        return purchases;
    }

    internal static ItemRosterElement[] RebaseInventoryData(
        ItemRoster authoritativeRoster,
        ItemRosterElement[] initialRoster,
        ItemRosterElement[] submittedRoster)
    {
        initialRoster ??= Array.Empty<ItemRosterElement>();
        submittedRoster ??= Array.Empty<ItemRosterElement>();
        var elements = new List<EquipmentElement>();
        AddUniqueElements(elements, initialRoster);
        AddUniqueElements(elements, submittedRoster);

        var rebased = new ItemRoster(authoritativeRoster);
        foreach (var element in elements)
        {
            long delta = (long)FindAmount(submittedRoster, element) - FindAmount(initialRoster, element);
            int current = FindAmount(rebased, element);
            int final = (int)Math.Max(0L, Math.Min(int.MaxValue, current + delta));
            if (final != current) rebased.AddToCounts(element, final - current);
        }
        return rebased.ToArray();
    }

    private static void AddUniqueElements(List<EquipmentElement> elements, IEnumerable<ItemRosterElement> roster)
    {
        foreach (var item in roster)
        {
            var element = item.EquipmentElement;
            if (element.Item == null || elements.Any(existing => SameElement(existing, element))) continue;
            elements.Add(element);
        }
    }

    private static int FindAmount(IEnumerable<ItemRosterElement> roster, EquipmentElement element)
    {
        foreach (var item in roster)
        {
            if (SameElement(item.EquipmentElement, element)) return item.Amount;
        }
        return 0;
    }

    private static bool SameElement(EquipmentElement left, EquipmentElement right)
        => left.Item == right.Item && left.ItemModifier == right.ItemModifier;

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
        else if (!VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(pool.SuppliesItems, message.BoughtItems, message.SoldItems, leftRemainder, out var error))
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
    /// Takes any purchased quantity the merchant can no longer supply back out of the party roster data, then the
    /// slots that gained it this trade, and refunds the prorated cost of what was taken back. Outputs each purchase's kept quantity.
    /// </summary>
    internal static int ReconcilePurchases(
        ItemRoster merchantRoster,
        ItemRosterElement[] partyRosterData,
        IEnumerable<HeroTradeEquipment> heroEquipments,
        List<(ItemRosterElement, int)> boughtItems,
        int totalAmount,
        out List<ItemRosterElement> keptItems)
    {
        keptItems = new List<ItemRosterElement>();

        foreach (var boughtItem in boughtItems)
        {
            var element = boughtItem.Item1.EquipmentElement;
            int amount = boughtItem.Item1.Amount;
            int elementIndex = merchantRoster.FindIndexOfElement(element);
            int available = elementIndex >= 0 ? merchantRoster.GetElementNumber(elementIndex) : 0;
            int shortfall = System.Math.Max(amount - available, 0);

            int removed = TakeFromRosterData(partyRosterData, element, shortfall);
            removed += TakeFromEquipment(heroEquipments, element, shortfall - removed);

            if (removed < shortfall)
            {
                logger.Warning("Trade kept {Missing} {Item} the merchant could not supply because they could not be taken back",
                    shortfall - removed, element.Item?.StringId);
            }

            totalAmount -= GetRefund(boughtItem.Item2, removed, amount);

            var kept = boughtItem.Item1;
            kept.Amount = amount - removed;
            keptItems.Add(kept);
        }

        return totalAmount;
    }

    // The prorated price of the purchased quantity taken back, shared by the gold charge and the exchange event so both agree
    private static int GetRefund(int price, int removed, int amount)
    {
        if (removed <= 0 || amount <= 0) return 0;

        return (int)((long)price * removed / amount);
    }

    /// <summary>
    /// Builds the merchant stock from the live roster with this trade's kept purchases removed and its sales added,
    /// so trades other players made since the client's snapshot are kept.
    /// </summary>
    internal static ItemRosterElement[] ApplyTradeToLiveStock(
        ItemRoster merchantRoster,
        IEnumerable<ItemRosterElement> keptItems,
        IEnumerable<(ItemRosterElement, int)> soldItems)
    {
        var stock = merchantRoster.Where(rosterElement => rosterElement.Amount > 0).ToList();

        foreach (var kept in keptItems) AddToStock(stock, kept.EquipmentElement, -kept.Amount);
        foreach (var (sold, _) in soldItems) AddToStock(stock, sold.EquipmentElement, sold.Amount);

        return stock.Where(rosterElement => rosterElement.Amount > 0).ToArray();
    }

    // Changes the amount of an element in the stock list without going below zero
    private static void AddToStock(List<ItemRosterElement> stock, EquipmentElement element, int amount)
    {
        int index = stock.FindIndex(rosterElement => rosterElement.EquipmentElement.IsEqualTo(element));
        if (index < 0)
        {
            if (amount > 0) stock.Add(new ItemRosterElement(element, amount));
            return;
        }

        var updated = stock[index];
        updated.Amount = System.Math.Max(updated.Amount + amount, 0);
        stock[index] = updated;
    }

    // Removes up to amount of the element from the roster data and returns how many were removed
    private static int TakeFromRosterData(ItemRosterElement[] rosterData, EquipmentElement element, int amount)
    {
        if (rosterData == null || amount <= 0) return 0;

        int index = System.Array.FindIndex(rosterData, rosterElement => rosterElement.EquipmentElement.IsEqualTo(element));
        if (index < 0) return 0;

        int removed = System.Math.Min(amount, System.Math.Max(rosterData[index].Amount, 0));
        rosterData[index].Amount -= removed;
        return removed;
    }

    // Empties up to amount of equipment slots that gained the element during this trade and returns how many were emptied
    private static int TakeFromEquipment(IEnumerable<HeroTradeEquipment> heroEquipments, EquipmentElement element, int amount)
    {
        if (heroEquipments == null || amount <= 0) return 0;

        int removed = 0;
        foreach (var heroEquipment in heroEquipments)
        {
            removed += heroEquipment.TakeGained(element, amount - removed);
        }

        return removed;
    }

    // Pairs each hero's traded equipment with the server's live equipment, which this trade has not been applied to yet
    private List<HeroTradeEquipment> GetHeroTradeEquipments(Dictionary<string, EquipmentData[]> characterIdEquipmentsData)
    {
        var heroEquipments = new List<HeroTradeEquipment>();
        if (characterIdEquipmentsData == null) return heroEquipments;

        foreach (var characterIdEquipment in characterIdEquipmentsData)
        {
            if (characterIdEquipment.Value == null) continue;
            if (!objectManager.TryGetObjectWithLogging<Hero>(characterIdEquipment.Key, out var hero)) continue;

            var tradedSets = new List<EquipmentElement[]>();
            var preTradeSets = new List<EquipmentElement[]>();
            foreach (var equipmentData in characterIdEquipment.Value)
            {
                var liveEquipment = GetLiveEquipment(hero.CharacterObject, equipmentData.EquipmentType);
                if (equipmentData.ItemSlots == null || liveEquipment == null) continue;

                tradedSets.Add(equipmentData.ItemSlots);
                preTradeSets.Add(liveEquipment._itemSlots);
            }

            heroEquipments.Add(new HeroTradeEquipment(tradedSets.ToArray(), preTradeSets.ToArray()));
        }

        return heroEquipments;
    }

    // Picks the hero's equipment set of the given type, the same sets UpdateEquipmentWithData writes
    private static Equipment GetLiveEquipment(CharacterObject character, Equipment.EquipmentType equipmentType)
    {
        if (equipmentType == Equipment.EquipmentType.Battle) return character.FirstBattleEquipment;
        if (equipmentType == Equipment.EquipmentType.Civilian) return character.FirstCivilianEquipment;
        if (equipmentType == Equipment.EquipmentType.Stealth) return character.FirstStealthEquipment;
        return null;
    }

    // Reads the merchant's current gold the same way the vanilla merchant and caravan inventory listeners do
    private static int GetLiveMerchantGold(SettlementComponent settlementComponent, MobileParty mobileParty, int fallback)
    {
        if (settlementComponent != null) return settlementComponent.Gold;
        if (mobileParty != null) return mobileParty.PartyTradeGold;
        return fallback;
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

    internal (ItemRosterElementData, int)[] ResolveLeftLootIds(ItemRoster items)
    {
        var resolvedItems = new List<(ItemRosterElementData, int)>();

        for (int i = 0; i < items.Count; i++)
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
