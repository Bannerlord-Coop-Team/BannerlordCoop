using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.Transactions;
using GameInterface.Services.Workshops.Interfaces;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
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
    private readonly IPlayerManager playerManager;
    private readonly ISessionWorkshopPlayerDataInterface workshopPlayerData;
    private readonly IBattleLootGrantRegistry battleLootGrants;

    public TradeHandler(
        IInventoryLogicInterface inventoryLogicInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        ISessionWorkshopPlayerDataInterface workshopPlayerData,
        IBattleLootGrantRegistry battleLootGrants)
    {
        this.inventoryLogicInterface = inventoryLogicInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.workshopPlayerData = workshopPlayerData;
        this.battleLootGrants = battleLootGrants;

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
        if (!objectManager.TryGetIdWithLogging(what.TroopRoster, out var troopRosterId)) return;
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
            soldItems = ResolveLeftLootIds(what.FromRoster.ToArray());
        }

        var characterIdEquipmentsData = ResolveCharacterIdEquipmentsData(what.OwnerParty, what.InitialCharacterEquipment);

        var troopRosterData = troopRosterInterface.PackTroopRosterData(what.TroopRoster);

        var message = new CompleteTrade(
            fromRosterId,
            fromRosterId is null,
            toRosterId,
            what.FromRoster.ToArray(),
            what.ToRoster.ToArray(),
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
            troopRosterId,
            troopRosterData
        );

        network.SendAll(message);
    }

    private void Handle_CompleteTrade(MessagePayload<CompleteTrade> payload)
    {
        var message = payload.What;
        var peer = payload.Who as NetPeer;

        GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
            peer, ServerTransactionOutcome.Trade, () =>
        {
            if (!ServerTransactionOutcome.TryResolvePlayer(
                    peer,
                    playerManager,
                    objectManager,
                    message.HeroId,
                    message.OwnerPartyId,
                    out var player,
                    out Hero hero,
                    out MobileParty ownerParty,
                    out string authenticationReason))
            {
                RejectTrade(peer, authenticationReason);
                return;
            }

            ItemRoster fromRoster = null;
            if (!message.IsFromItemRosterNull && !objectManager.TryGetObjectWithLogging<ItemRoster>(message.FromItemRosterId, out fromRoster))
            {
                RejectTrade(peer, "The merchant inventory is no longer available.");
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(message.ToItemRosterId, out var toRoster) ||
                !objectManager.TryGetObjectWithLogging<TroopRoster>(message.TroopRosterId, out var troopRoster) ||
                !objectManager.TryGetObjectWithLogging<Hero>(message.InitialHeroId, out var initialHero))
            {
                RejectTrade(peer, "Your trade state is no longer available.");
                return;
            }

            if (!ReferenceEquals(toRoster, ownerParty.ItemRoster) ||
                !ReferenceEquals(troopRoster, ownerParty.MemberRoster) ||
                initialHero != hero &&
                (initialHero.Clan != hero.Clan ||
                 initialHero.PartyBelongedTo != ownerParty))
            {
                RejectTrade(peer, "Trade ownership did not match the connected player.");
                return;
            }

            SettlementComponent currentSettlementComponent = null;
            if (!message.IsSettlementComponentNull &&
                !objectManager.TryGetObjectWithLogging<SettlementComponent>(message.CurrentSettlementComponentId, out currentSettlementComponent))
            {
                RejectTrade(peer, "The trading settlement is no longer available.");
                return;
            }

            MobileParty currentMobileParty = null;
            if (message.CurrentMobilePartyId != null && !objectManager.TryGetObjectWithLogging<MobileParty>(message.CurrentMobilePartyId, out currentMobileParty))
            {
                RejectTrade(peer, "The trading party is no longer available.");
                return;
            }

            var boughtItems = ResolveTradeItems(message.BoughtItems);
            var soldItems = ResolveTradeItems(message.SoldItems);
            if (boughtItems.Count != (message.BoughtItems?.Length ?? 0) ||
                soldItems.Count != (message.SoldItems?.Length ?? 0) ||
                message.CharacterIdEquipmentsData == null)
            {
                RejectTrade(peer, "The submitted trade contained invalid items.");
                return;
            }
            ResolveCharacterEquipmentsData(message.CharacterIdEquipmentsData, out var characterEquipmentsData);

            BattleLootClaim battleLootClaim = null;
            bool isBattleLootClaim = false;
            if (!message.IsTrading &&
                !message.IsManagingWarehouse &&
                fromRoster == null &&
                currentMobileParty == null &&
                currentSettlementComponent == null)
            {
                BattleLootClaimStatus claimStatus = battleLootGrants.TryBeginClaim(
                    player.ControllerId,
                    player.HeroId,
                    message.OwnerPartyId,
                    boughtItems.Select(item => item.Item1),
                    message.FromItemRosterData,
                    out battleLootClaim,
                    out string claimReason);
                if (claimStatus == BattleLootClaimStatus.Rejected)
                {
                    RejectTrade(peer, claimReason);
                    return;
                }
                isBattleLootClaim = claimStatus == BattleLootClaimStatus.Accepted;
            }

            List<(ItemRosterElement, int)> ownedSoldItems = isBattleLootClaim
                ? battleLootClaim.ReturnedOwnedItems
                    .Select(item => (item, 0))
                    .ToList()
                : soldItems;
            List<(ItemRosterElement, int)> postEffectSoldItems = isBattleLootClaim
                ? battleLootClaim.DiscardedItems
                    .Select(item => (item, 0))
                    .ToList()
                : soldItems;

            if (!TryValidateTrade(
                    message,
                    hero,
                    ownerParty,
                    fromRoster,
                    toRoster,
                    currentMobileParty,
                    currentSettlementComponent,
                    initialHero,
                    characterEquipmentsData,
                    boughtItems,
                    ownedSoldItems,
                    isBattleLootClaim,
                    out int totalAmount,
                    out int merchantGold,
                    out List<(ItemRosterElement, int)> authoritativeBoughtItems,
                    out List<(ItemRosterElement, int)> authoritativeSoldItems,
                    out ItemRosterElement[] authoritativeOwnedRoster,
                    out string validationReason))
            {
                battleLootGrants.Release(battleLootClaim);
                RejectTrade(peer, validationReason);
                return;
            }
            if (message.IsTrading && totalAmount > hero.Gold)
            {
                battleLootGrants.Release(battleLootClaim);
                RejectTrade(peer, "You no longer have enough denars for this trade.");
                return;
            }
            boughtItems = authoritativeBoughtItems;
            soldItems = isBattleLootClaim
                ? postEffectSoldItems
                : authoritativeSoldItems;

            ItemRosterElement[] fromItemRosterData = NormalizeRosterData(
                message.FromItemRosterData);
            ItemRosterElement[] toItemRosterData = authoritativeOwnedRoster;
            ItemRosterElement[] toRosterBefore = toRoster?.ToArray();
            ItemRosterElement[] fromRosterBefore = fromRoster?.ToArray();
            Dictionary<CharacterObject, Equipment[]> equipmentBefore =
                CloneEquipment(characterEquipmentsData.Keys);
            int heroGoldBefore = hero.Gold;
            int settlementGoldBefore = currentSettlementComponent?.Gold ?? 0;
            Hero merchantHero = currentMobileParty?.Party.LeaderHero;
            int merchantHeroGoldBefore = merchantHero?.Gold ?? 0;
            int merchantPartyGoldBefore =
                currentMobileParty?.PartyTradeGold ?? 0;
            string warehouseSettlementId = null;
            ItemRosterElement[] warehouseBefore = null;
            if (message.IsManagingWarehouse &&
                currentSettlementComponent?.Settlement != null &&
                objectManager.TryGetId(
                    currentSettlementComponent.Settlement,
                    out warehouseSettlementId))
            {
                warehouseBefore = workshopPlayerData.GetWarehouseRoster(
                    message.HeroId, warehouseSettlementId).ToArray();
            }

            try
            {
                if (toRoster != null)
                    inventoryLogicInterface.UpdateRosterWithData(
                        toRoster, toItemRosterData);
                if (fromRoster != null && message.IsTrading)
                {
                    foreach (var boughtItem in boughtItems)
                        fromRoster.AddToCounts(
                            boughtItem.Item1.EquipmentElement,
                            -boughtItem.Item1.Amount);
                    foreach (var soldItem in soldItems)
                        fromRoster.AddToCounts(
                            soldItem.Item1.EquipmentElement,
                            soldItem.Item1.Amount);
                }
                else if (fromRoster != null)
                    inventoryLogicInterface.UpdateRosterWithData(
                        fromRoster, fromItemRosterData);
                else if (warehouseBefore != null)
                    workshopPlayerData.UpdateWarehouseRoster(
                        message.HeroId,
                        warehouseSettlementId,
                        fromItemRosterData);

                inventoryLogicInterface.UpdateEquipmentWithData(
                    ownerParty, characterEquipmentsData, initialHero);
                inventoryLogicInterface.ApplyTradeGold(
                    fromRoster,
                    toRoster,
                    message.IsTrading,
                    hero,
                    totalAmount,
                    merchantGold,
                    currentMobileParty,
                    currentSettlementComponent);
                if (!battleLootGrants.Consume(battleLootClaim))
                    throw new InvalidOperationException(
                        "The server battle-loot grant changed during its authoritative commit.");
                battleLootClaim = null;
            }
            catch (Exception exception)
            {
                bool rollbackSucceeded = false;
                try
                {
                    hero.Gold = heroGoldBefore;
                    if (currentSettlementComponent != null)
                        currentSettlementComponent.ChangeGold(
                            settlementGoldBefore -
                            currentSettlementComponent.Gold);
                    if (merchantHero != null)
                        merchantHero.Gold = merchantHeroGoldBefore;
                    if (currentMobileParty != null)
                        currentMobileParty.PartyTradeGold =
                            merchantPartyGoldBefore;
                    if (toRoster != null)
                        inventoryLogicInterface.UpdateRosterWithData(
                            toRoster, toRosterBefore);
                    if (fromRoster != null)
                        inventoryLogicInterface.UpdateRosterWithData(
                            fromRoster, fromRosterBefore);
                    if (warehouseBefore != null)
                        workshopPlayerData.UpdateWarehouseRoster(
                            message.HeroId,
                            warehouseSettlementId,
                            warehouseBefore);
                    inventoryLogicInterface.UpdateEquipmentWithData(
                        ownerParty, equipmentBefore, initialHero);
                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    logger.Error(
                        rollbackException,
                        "Trade rollback failed for {HeroId}",
                        message.HeroId);
                }
                if (rollbackSucceeded)
                {
                    battleLootGrants.Release(battleLootClaim);
                }
                else if (!battleLootGrants.Consume(battleLootClaim))
                {
                    logger.Error(
                        "Failed to forfeit the battle-loot grant after an incomplete rollback for {HeroId}",
                        message.HeroId);
                }
                logger.Error(
                    exception,
                    "Trade core commit failed for {HeroId}",
                    message.HeroId);
                RejectTrade(
                    peer,
                    "The trade could not be committed safely. Please try again.");
                return;
            }

            TryPostTradeEffect(() => network.SendAll(
                new UpdateEquipmentClients(
                    message.CharacterIdEquipmentsData,
                    message.OwnerPartyId,
                    message.InitialHeroId)), "equipment broadcast");
            if (warehouseBefore != null)
                TryPostTradeEffect(() => network.Send(
                    peer,
                    new ManageWarehouseRoster(
                        warehouseSettlementId,
                        fromItemRosterData)), "warehouse broadcast");
            TryPostTradeEffect(() => inventoryLogicInterface
                .ApplyPostTradeEffects(
                    message.IsTrading,
                    message.CanGainXpFromDiscarding &&
                        !message.IsTrading &&
                        !message.IsManagingWarehouse &&
                        fromRoster == null &&
                        currentMobileParty == null &&
                        currentSettlementComponent == null,
                    hero,
                    totalAmount,
                    currentMobileParty,
                    currentSettlementComponent,
                    boughtItems,
                    soldItems), "trade progression effects");
            ServerTransactionOutcome.Accept(
                peer, ServerTransactionOutcome.Trade);
        }));
    }

    private bool TryValidateTrade(
        CompleteTrade message,
        Hero hero,
        MobileParty ownerParty,
        ItemRoster fromRoster,
        ItemRoster toRoster,
        MobileParty currentMobileParty,
        SettlementComponent currentSettlementComponent,
        Hero initialHero,
        Dictionary<CharacterObject, Equipment[]> submittedEquipment,
        List<(ItemRosterElement, int)> boughtItems,
        List<(ItemRosterElement, int)> soldItems,
        bool isBattleLootClaim,
        out int totalAmount,
        out int merchantGold,
        out List<(ItemRosterElement, int)> authoritativeBoughtItems,
        out List<(ItemRosterElement, int)> authoritativeSoldItems,
        out ItemRosterElement[] authoritativeOwnedRoster,
        out string reason)
    {
        totalAmount = 0;
        merchantGold = 0;
        authoritativeBoughtItems = boughtItems;
        authoritativeSoldItems = soldItems;
        authoritativeOwnedRoster = null;
        reason = "The trade no longer matches the server state.";

        if (fromRoster != null && ReferenceEquals(fromRoster, toRoster))
        {
            reason = "The source and destination inventory cannot be the same roster.";
            return false;
        }

        if (boughtItems.Any(item =>
                item.Item1.EquipmentElement.IsQuestItem) ||
            soldItems.Any(item =>
                item.Item1.EquipmentElement.IsQuestItem))
        {
            reason = "Quest items cannot be traded or moved from their inventory.";
            return false;
        }

        if (toRoster == null || !TryBuildAuthoritativeOwnedInventory(
                ownerParty,
                initialHero,
                message.ToItemRosterData,
                submittedEquipment,
                boughtItems,
                soldItems,
                out authoritativeOwnedRoster))
        {
            reason = "The submitted inventory did not match the transferred items.";
            return false;
        }

        if (!message.IsTrading)
        {
            if (message.IsManagingWarehouse)
            {
                Settlement settlement = currentSettlementComponent?.Settlement;
                if (settlement == null || ownerParty.CurrentSettlement != settlement ||
                    !objectManager.TryGetId(settlement, out string settlementId) ||
                    !TryValidateExternalRosterSnapshot(
                        workshopPlayerData.GetWarehouseRoster(
                            message.HeroId, settlementId),
                        message.FromItemRosterData,
                        boughtItems,
                        soldItems))
                {
                    reason = "The warehouse inventory changed before the transfer completed.";
                    return false;
                }
            }
            else if (fromRoster != null)
            {
                if (!IsAuthorizedNonTradingSource(
                        hero,
                        ownerParty,
                        fromRoster,
                        currentMobileParty,
                        currentSettlementComponent) ||
                    !TryValidateExternalRosterSnapshot(
                        fromRoster,
                        message.FromItemRosterData,
                        boughtItems,
                        soldItems))
                {
                    reason = "The other inventory changed before the transfer completed.";
                    return false;
                }
            }
            else if (boughtItems.Count != 0 && !isBattleLootClaim)
            {
                reason = "The transferred items had no authoritative source.";
                return false;
            }
            return true;
        }

        if (fromRoster == null)
            return false;

        PartyBase merchantParty;
        IMarketData marketData;
        TownMarketData pricingMarketData = null;
        if (currentSettlementComponent != null)
        {
            Settlement settlement = currentSettlementComponent.Settlement;
            if (settlement == null || ownerParty.CurrentSettlement != settlement ||
                !ReferenceEquals(fromRoster, settlement.ItemRoster))
            {
                reason = "You are no longer trading in that settlement.";
                return false;
            }
            merchantParty = currentSettlementComponent.Owner;
            merchantGold = currentSettlementComponent.Gold;
            marketData = settlement.IsVillage
                ? settlement.Village?.MarketData
                : settlement.Town?.MarketData;
            pricingMarketData = settlement.IsVillage
                ? null
                : settlement.Town?.MarketData;
        }
        else if (currentMobileParty != null)
        {
            if (ReferenceEquals(currentMobileParty, ownerParty) ||
                !ReferenceEquals(fromRoster, currentMobileParty.ItemRoster) ||
                !IsMobilePartyInteractionAvailable(
                    ownerParty, currentMobileParty))
            {
                reason = "The trading party no longer matches this interaction.";
                return false;
            }
            merchantParty = currentMobileParty.Party;
            merchantGold = currentMobileParty.PartyTradeGold;
            Settlement nearest = SettlementHelper
                .FindNearestTownToMobileParty(
                    ownerParty,
                    MobileParty.NavigationType.All)?.Settlement;
            marketData = nearest?.Town?.MarketData;
        }
        else
        {
            reason = "The merchant is no longer available.";
            return false;
        }

        if (!TryBuildAuthoritativeTradeHistory(
                ownerParty,
                merchantParty,
                marketData,
                pricingMarketData,
                merchantGold,
                boughtItems,
                soldItems,
                out totalAmount,
                out authoritativeBoughtItems,
                out authoritativeSoldItems,
                out reason))
            return false;

        foreach (var bought in boughtItems
                     .GroupBy(item => item.Item1.EquipmentElement)
                     .Select(group => new
                     {
                         Element = group.Key,
                         Amount = group.Sum(item => (long)item.Item1.Amount)
                     }))
        {
            int index = fromRoster.FindIndexOfElement(
                bought.Element);
            if (index < 0 ||
                bought.Amount > int.MaxValue ||
                fromRoster.GetElementCopyAtIndex(index).Amount < bought.Amount)
            {
                reason = "One or more purchased items are no longer in stock.";
                return false;
            }
        }

        return true;
    }

    private static bool IsMobilePartyInteractionAvailable(
        MobileParty ownerParty,
        MobileParty merchantParty)
    {
        if (ownerParty?.IsActive != true ||
            merchantParty?.IsActive != true ||
            merchantParty.Party?.IsActive != true ||
            ownerParty.MapEvent != null ||
            merchantParty.MapEvent != null ||
            merchantParty.IsBandit ||
            !(merchantParty.IsLordParty ||
              merchantParty.IsCaravan ||
              merchantParty.IsVillager) ||
            ownerParty.MapFaction == null ||
            merchantParty.MapFaction == null ||
            FactionManager.IsAtWarAgainstFaction(
                ownerParty.MapFaction,
                merchantParty.MapFaction))
            return false;
        if (ownerParty.CurrentSettlement != null &&
            ownerParty.CurrentSettlement == merchantParty.CurrentSettlement)
            return true;
        if (ownerParty.Army != null && ownerParty.Army == merchantParty.Army)
            return true;
        if (ownerParty.CurrentSettlement != null ||
            merchantParty.CurrentSettlement != null)
            return false;

        float radius = Campaign.Current.Models.EncounterModel
            .GetEncounterJoiningRadius;
        return ownerParty.Position.ToVec2().Distance(
            merchantParty.Position.ToVec2()) <= radius;
    }

    private static bool IsAuthorizedNonTradingSource(
        Hero hero,
        MobileParty ownerParty,
        ItemRoster fromRoster,
        MobileParty currentMobileParty,
        SettlementComponent currentSettlementComponent)
    {
        if (ownerParty == null || fromRoster == null)
            return false;
        if (ReferenceEquals(fromRoster, ownerParty.ItemRoster))
            return true;

        Settlement settlement = currentSettlementComponent?.Settlement ??
            ownerParty.CurrentSettlement;
        if (settlement != null && ownerParty.CurrentSettlement == settlement &&
            settlement.OwnerClan == hero?.Clan &&
            ReferenceEquals(fromRoster, settlement.Stash))
            return true;

        if (currentMobileParty == null ||
            !ReferenceEquals(fromRoster, currentMobileParty.ItemRoster) ||
            currentMobileParty.ActualClan == null ||
            currentMobileParty.ActualClan != hero?.Clan)
            return false;
        if (currentMobileParty.MapEvent != null &&
            currentMobileParty.MapEvent == ownerParty.MapEvent)
            return true;
        if (currentMobileParty.Army != null &&
            currentMobileParty.Army == ownerParty.Army)
            return true;

        float radius = Campaign.Current.Models.EncounterModel
            .GetEncounterJoiningRadius;
        return ownerParty.Position.ToVec2().Distance(
            currentMobileParty.Position.ToVec2()) <= radius;
    }

    private static bool TryValidateExternalRosterSnapshot(
        IEnumerable<ItemRosterElement> currentRoster,
        ItemRosterElement[] submittedRoster,
        IEnumerable<(ItemRosterElement, int)> takenFromExternal,
        IEnumerable<(ItemRosterElement, int)> givenToExternal)
    {
        if (currentRoster == null || submittedRoster == null)
            return false;
        if (submittedRoster.Any(element =>
                element.EquipmentElement.Item == null
                    ? element.Amount != 0
                    : element.Amount < 0))
            return false;
        Dictionary<EquipmentElement, long> expected = CountOwnedItems(
            currentRoster, Enumerable.Empty<Equipment>());
        foreach (var item in takenFromExternal)
            AddOwnedCount(
                expected, item.Item1.EquipmentElement, -item.Item1.Amount);
        foreach (var item in givenToExternal)
            AddOwnedCount(
                expected, item.Item1.EquipmentElement, item.Item1.Amount);
        Dictionary<EquipmentElement, long> submitted = CountOwnedItems(
            submittedRoster, Enumerable.Empty<Equipment>());
        foreach (EquipmentElement empty in expected
                     .Where(pair => pair.Value == 0)
                     .Select(pair => pair.Key)
                     .ToArray())
            expected.Remove(empty);
        return expected.Count == submitted.Count && expected.All(pair =>
            pair.Value >= 0 && submitted.TryGetValue(pair.Key, out long value) &&
            value == pair.Value);
    }

    private static bool TryBuildAuthoritativeTradeHistory(
        MobileParty ownerParty,
        PartyBase merchantParty,
        IMarketData marketData,
        TownMarketData pricingMarketData,
        int merchantGold,
        IEnumerable<(ItemRosterElement, int)> boughtItems,
        IEnumerable<(ItemRosterElement, int)> soldItems,
        out int totalAmount,
        out List<(ItemRosterElement, int)> authoritativeBoughtItems,
        out List<(ItemRosterElement, int)> authoritativeSoldItems,
        out string reason)
    {
        totalAmount = 0;
        authoritativeBoughtItems = new List<(ItemRosterElement, int)>();
        authoritativeSoldItems = new List<(ItemRosterElement, int)>();
        reason = "The submitted trade values were invalid.";
        var marketState = new Dictionary<ItemCategory, ItemData>();
        long boughtTotal = 0;
        long soldTotal = 0;
        foreach (var bought in boughtItems)
        {
            if (bought.Item1.Amount <= 0 ||
                bought.Item1.EquipmentElement.Item == null)
                return false;
            if (!TryPriceTradeRow(
                    bought.Item1,
                    false,
                    ownerParty,
                    merchantParty,
                    marketData,
                    pricingMarketData,
                    marketState,
                    out int rowTotal))
                return false;
            boughtTotal += rowTotal;
            authoritativeBoughtItems.Add((bought.Item1, rowTotal));
        }
        foreach (var sold in soldItems)
        {
            if (sold.Item1.Amount <= 0 ||
                sold.Item1.EquipmentElement.Item == null)
                return false;
            if (!TryPriceTradeRow(
                    sold.Item1,
                    true,
                    ownerParty,
                    merchantParty,
                    marketData,
                    pricingMarketData,
                    marketState,
                    out int rowTotal))
                return false;
            soldTotal += rowTotal;
            authoritativeSoldItems.Add((sold.Item1, rowTotal));
        }

        long rawTotal = boughtTotal - soldTotal;
        long expectedTotal = rawTotal < 0
            ? Math.Max(rawTotal, -(long)Math.Max(0, merchantGold))
            : rawTotal;
        if (expectedTotal < int.MinValue || expectedTotal > int.MaxValue)
            return false;
        totalAmount = (int)expectedTotal;
        return true;
    }

    private static bool TryPriceTradeRow(
        ItemRosterElement row,
        bool isSelling,
        MobileParty ownerParty,
        PartyBase merchantParty,
        IMarketData marketData,
        TownMarketData pricingMarketData,
        IDictionary<ItemCategory, ItemData> marketState,
        out int rowTotal)
    {
        rowTotal = 0;
        ItemObject item = row.EquipmentElement.Item;
        ItemCategory category = item?.GetItemCategory();
        long total = 0;
        for (int index = 0; index < row.Amount; index++)
        {
            int price;
            if (pricingMarketData != null && category != null)
            {
                if (!marketState.TryGetValue(category, out ItemData state))
                    state = pricingMarketData.GetCategoryData(category);
                price = Campaign.Current.Models.TradeItemPriceFactorModel.GetPrice(
                    row.EquipmentElement,
                    ownerParty,
                    merchantParty,
                    isSelling,
                    state.InStoreValue,
                    state.Supply,
                    state.Demand);
                long nextInStore = state.InStore + (isSelling ? 1L : -1L);
                long nextInStoreValue = state.InStoreValue +
                    (isSelling ? (long)item.Value : -(long)item.Value);
                if (nextInStore < int.MinValue || nextInStore > int.MaxValue ||
                    nextInStoreValue < int.MinValue ||
                    nextInStoreValue > int.MaxValue)
                    return false;
                marketState[category] = new ItemData(
                    state.Supply,
                    state.Demand,
                    (int)nextInStore,
                    (int)nextInStoreValue);
            }
            else
            {
                price = marketData?.GetPrice(
                    row.EquipmentElement,
                    ownerParty,
                    isSelling,
                    merchantParty) ?? row.EquipmentElement.ItemValue;
            }
            total += Math.Max(0, price);
            if (total > int.MaxValue)
                return false;
        }
        rowTotal = (int)total;
        return true;
    }

    private static bool TryBuildAuthoritativeOwnedInventory(
        MobileParty ownerParty,
        Hero initialHero,
        ItemRosterElement[] submittedRoster,
        Dictionary<CharacterObject, Equipment[]> submittedEquipment,
        IEnumerable<(ItemRosterElement, int)> boughtItems,
        IEnumerable<(ItemRosterElement, int)> soldItems,
        out ItemRosterElement[] authoritativeRoster)
    {
        authoritativeRoster = null;
        if (submittedRoster == null || submittedEquipment == null)
            return false;
        if (submittedRoster.Any(element =>
                element.EquipmentElement.Item == null
                    ? element.Amount != 0
                    : element.Amount < 0))
            return false;

        var expectedCharacters = new HashSet<CharacterObject>();
        foreach (TroopRosterElement element in ownerParty.MemberRoster.GetTroopRoster())
        {
            if (element.Character?.IsHero == true)
                expectedCharacters.Add(element.Character);
        }
        if (initialHero?.CharacterObject != null)
            expectedCharacters.Add(initialHero.CharacterObject);
        if (submittedEquipment.Keys.Any(
                character => !expectedCharacters.Contains(character)) ||
            submittedEquipment.Values.Any(
                equipments => equipments == null || equipments.Length != 3 ||
                    equipments.Count(equipment =>
                        equipment?._equipmentType ==
                            Equipment.EquipmentType.Battle) != 1 ||
                    equipments.Count(equipment =>
                        equipment?._equipmentType ==
                            Equipment.EquipmentType.Civilian) != 1 ||
                    equipments.Count(equipment =>
                        equipment?._equipmentType ==
                            Equipment.EquipmentType.Stealth) != 1))
            return false;

        // Build the transaction on the current server state, not on the client's
        // inventory-screen opening snapshot. Food consumption and other authoritative
        // party updates can legitimately occur while the inventory screen is open.
        // The submitted final equipment remains an intent; the final item roster is
        // derived here so a stale client can neither overwrite newer state nor duplicate
        // an item by racing an asynchronous roster update.
        Dictionary<EquipmentElement, long> available = CountOwnedItems(
            ownerParty.ItemRoster,
            expectedCharacters.SelectMany(character => new[]
            {
                character.FirstBattleEquipment,
                character.FirstCivilianEquipment,
                character.FirstStealthEquipment
            }));
        foreach (var bought in boughtItems)
        {
            if (bought.Item1.EquipmentElement.Item == null ||
                bought.Item1.Amount <= 0 ||
                !TryAddOwnedCount(
                    available,
                    bought.Item1.EquipmentElement,
                    bought.Item1.Amount))
                return false;
        }
        foreach (var sold in soldItems)
        {
            if (sold.Item1.EquipmentElement.Item == null ||
                sold.Item1.Amount <= 0 ||
                !TryAddOwnedCount(
                    available,
                    sold.Item1.EquipmentElement,
                    -(long)sold.Item1.Amount))
                return false;
        }

        IEnumerable<Equipment> finalEquipment =
            expectedCharacters.SelectMany(character =>
                submittedEquipment.TryGetValue(
                    character, out Equipment[] equipments)
                    ? equipments
                    : new[]
                    {
                        character.FirstBattleEquipment,
                        character.FirstCivilianEquipment,
                        character.FirstStealthEquipment
                    });
        foreach (Equipment equipment in finalEquipment)
        {
            if (equipment == null)
                continue;
            for (int index = 0; index < Equipment.EquipmentSlotLength; index++)
            {
                EquipmentElement element = equipment[(EquipmentIndex)index];
                if (element.Item != null &&
                    (!TryAddOwnedCount(available, element, -1) ||
                     available[element] < 0))
                    return false;
            }
        }

        var result = new List<ItemRosterElement>();
        foreach (KeyValuePair<EquipmentElement, long> item in available)
        {
            if (item.Value < 0 || item.Value > int.MaxValue)
                return false;
            if (item.Value > 0)
                result.Add(new ItemRosterElement(item.Key, (int)item.Value));
        }
        authoritativeRoster = result.ToArray();
        return true;
    }

    private static Dictionary<EquipmentElement, long> CountOwnedItems(
        IEnumerable<ItemRosterElement> roster,
        IEnumerable<Equipment> equipments)
    {
        var result = new Dictionary<EquipmentElement, long>();
        foreach (ItemRosterElement element in roster ??
                     Enumerable.Empty<ItemRosterElement>())
        {
            if (element.EquipmentElement.Item == null || element.Amount < 0)
                continue;
            AddOwnedCount(result, element.EquipmentElement, element.Amount);
        }
        foreach (Equipment equipment in equipments ?? Enumerable.Empty<Equipment>())
        {
            if (equipment == null)
                continue;
            for (int index = 0; index < Equipment.EquipmentSlotLength; index++)
            {
                EquipmentElement element = equipment[(EquipmentIndex)index];
                if (element.Item != null)
                    AddOwnedCount(result, element, 1);
            }
        }
        foreach (EquipmentElement empty in result
                     .Where(pair => pair.Value == 0)
                     .Select(pair => pair.Key)
                     .ToArray())
            result.Remove(empty);
        return result;
    }

    private static ItemRosterElement[] NormalizeRosterData(
        IEnumerable<ItemRosterElement> roster)
    {
        return (roster ?? Enumerable.Empty<ItemRosterElement>())
            .Where(element =>
                element.EquipmentElement.Item != null && element.Amount > 0)
            .ToArray();
    }

    private static void AddOwnedCount(
        IDictionary<EquipmentElement, long> counts,
        EquipmentElement element,
        long change)
    {
        counts.TryGetValue(element, out long current);
        counts[element] = current + change;
    }

    private static bool TryAddOwnedCount(
        IDictionary<EquipmentElement, long> counts,
        EquipmentElement element,
        long change)
    {
        counts.TryGetValue(element, out long current);
        try
        {
            counts[element] = checked(current + change);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void RejectTrade(NetPeer peer, string reason)
    {
        ServerTransactionOutcome.Reject(
            peer, ServerTransactionOutcome.Trade, reason);
    }

    private static Dictionary<CharacterObject, Equipment[]> CloneEquipment(
        IEnumerable<CharacterObject> characters)
    {
        return characters.ToDictionary(
            character => character,
            character => new[]
            {
                new Equipment(character.FirstBattleEquipment),
                new Equipment(character.FirstCivilianEquipment),
                new Equipment(character.FirstStealthEquipment)
            });
    }

    private static void TryPostTradeEffect(Action action, string operation)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "Trade committed, but {Operation} failed",
                operation);
        }
    }

    private void Handle_UpdateEquipmentClients(MessagePayload<UpdateEquipmentClients> obj)
    {
        if (ModInformation.IsServer) return;

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
