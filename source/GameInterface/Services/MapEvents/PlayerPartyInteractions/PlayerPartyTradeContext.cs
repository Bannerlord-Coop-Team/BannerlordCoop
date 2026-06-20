using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal static class PlayerPartyTradeContext
{
    public static string SessionId { get; private set; }
    public static bool IsActive => SessionId != null;
    public static PartyBase LocalParty { get; private set; }
    public static bool LocalAccepted { get; private set; }
    public static bool RemoteAccepted { get; private set; }
    public static bool SuppressNativeCloseMessages { get; private set; }

    private static BarterVM activeBarterVM;
    private static ButtonWidget resetButton;
    private static bool isApplyingServerOffer;
    private static readonly FieldInfo PrisonerCharacterField =
        typeof(TransferPrisonerBarterable).GetField("_prisonerCharacter", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly HashSet<BarterItemVM> scratchItems = new HashSet<BarterItemVM>();

    public static void Begin(string sessionId, PartyBase localParty = null)
    {
        SessionId = sessionId;
        LocalParty = localParty;
        LocalAccepted = false;
        RemoteAccepted = false;
        activeBarterVM = null;
        resetButton = null;
    }

    public static void End(string sessionId = null, PlayerPartyInteractionOutcomeType outcomeType = PlayerPartyInteractionOutcomeType.None)
    {
        if (sessionId != null && SessionId != null && SessionId != sessionId) return;

        var barterVM = activeBarterVM;

        SessionId = null;
        LocalParty = null;
        LocalAccepted = false;
        RemoteAccepted = false;
        activeBarterVM = null;
        resetButton = null;

        try
        {
            SuppressNativeCloseMessages = barterVM != null;
            barterVM?.ExecuteCancel();
        }
        catch (NullReferenceException)
        {
        }
        finally
        {
            SuppressNativeCloseMessages = false;
        }

        ShowOutcomeMessage(outcomeType);
    }

    public static void SetBarterVM(BarterVM barterVM)
    {
        if (!IsActive) return;

        activeBarterVM = barterVM;
        RefreshBarterControls();
    }

    public static void UpdateAcceptance(bool localAccepted, bool remoteAccepted)
    {
        if (!IsActive) return;

        LocalAccepted = localAccepted;
        RemoteAccepted = remoteAccepted;
        RefreshBarterControls();
    }

    public static bool CanTransfer(TransferCommand transferCommand)
    {
        if (!IsActive) return true;
        if (!CanModifyOffer()) return false;

        return transferCommand.FromSide == InventoryLogic.InventorySide.PlayerInventory;
    }

    public static bool CanOffer(Barterable barterable)
    {
        if (!IsActive) return true;
        if (!CanModifyOffer()) return false;
        if (barterable == null || LocalParty == null) return false;
        if (barterable is FiefBarterable fiefBarterable)
            return IsOwnedByLocalParty(fiefBarterable);

        return barterable.OriginalParty == LocalParty;
    }

    public static bool CanAccept()
        => !IsActive || !LocalAccepted;

    public static bool CanCancel()
        => !IsActive || !LocalAccepted;

    public static bool CanReset()
        => !IsActive;

    public static bool CanModifyOffer()
        => !IsActive || (!LocalAccepted && !RemoteAccepted);

    public static void SetResetButton(ButtonWidget buttonWidget)
    {
        if (!IsActive) return;

        resetButton = buttonWidget;
        RefreshResetButton();
    }

    public static bool CanOfferTransferAll(BarterVM barterVM, string methodName)
    {
        if (!IsActive) return true;
        if (barterVM == null || !CanModifyOffer()) return false;

        switch (methodName)
        {
            case nameof(BarterVM.ExecuteTransferAllLeftFief):
                return CanOfferAny(barterVM.LeftFiefList);
            case nameof(BarterVM.ExecuteTransferAllLeftItem):
                return CanOfferAny(barterVM.LeftItemList);
            case nameof(BarterVM.ExecuteTransferAllLeftPrisoner):
                return CanOfferAny(barterVM.LeftPrisonerList);
            case nameof(BarterVM.ExecuteTransferAllLeftOther):
                return CanOfferAny(barterVM.LeftOtherList);
            case nameof(BarterVM.ExecuteTransferAllRightFief):
                return CanOfferAny(barterVM.RightFiefList);
            case nameof(BarterVM.ExecuteTransferAllRightItem):
                return CanOfferAny(barterVM.RightItemList);
            case nameof(BarterVM.ExecuteTransferAllRightPrisoner):
                return CanOfferAny(barterVM.RightPrisonerList);
            case nameof(BarterVM.ExecuteTransferAllRightOther):
                return CanOfferAny(barterVM.RightOtherList);
            case nameof(BarterVM.ExecuteTransferAllGoldLeft):
                return CanOfferAny(barterVM.LeftGoldList);
            case nameof(BarterVM.ExecuteTransferAllGoldRight):
                return CanOfferAny(barterVM.RightGoldList);
            default:
                return false;
        }
    }

    public static void PublishOfferChanged(InventoryLogic inventoryLogic)
    {
        if (!IsActive) return;
        if (isApplyingServerOffer || !CanModifyOffer()) return;

        MessageBroker.Instance.Publish(inventoryLogic, new PlayerPartyTradeOfferChanged(SessionId, inventoryLogic));
    }

    public static void PublishOfferChanged(BarterVM barterVM)
    {
        if (!IsActive) return;
        if (isApplyingServerOffer || !CanModifyOffer()) return;

        MessageBroker.Instance.Publish(barterVM, new PlayerPartyTradeOfferChanged(SessionId, barterVM));
    }

    public static void PublishAccept(bool accepted)
    {
        if (!IsActive) return;
        if (!CanAccept()) return;

        if (accepted)
        {
            LocalAccepted = true;
            RefreshBarterControls();
        }

        MessageBroker.Instance.Publish(null, new PlayerPartyTradeAcceptSelected(SessionId, accepted));
    }

    public static void PublishLeave()
    {
        if (!IsActive) return;
        if (!CanCancel()) return;

        MessageBroker.Instance.Publish(
            null,
            new PlayerPartyInteractionOptionSelected(
                SessionId,
                PlayerPartyInteractionDialogState.PartyId,
                PlayerPartyInteractionOption.Leave));
    }

    public static void ApplyOfferUpdate(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        if (!IsActive || SessionId != message.SessionId) return;
        if (activeBarterVM == null || objectManager == null) return;

        if (IsLocalOfferMessage(message, objectManager))
        {
            RefreshBarterControls();
            return;
        }

        isApplyingServerOffer = true;

        try
        {
            ApplyItemOffers(message, objectManager);
            ApplyGoldOffer(message, objectManager);
            ApplyFiefOffers(message, objectManager);
            ApplyPrisonerOffers(message, objectManager);
            ApplyTroopOffers(message, objectManager);
            RefreshBarterControls();
        }
        finally
        {
            isApplyingServerOffer = false;
        }
    }

    public static void RefreshBarterControls()
    {
        if (activeBarterVM == null) return;

        activeBarterVM.DiplomaticLbl = string.Empty;
        activeBarterVM.OtherLbl = "Troops";
        activeBarterVM.LeftDiplomaticList?.Clear();
        activeBarterVM.RightDiplomaticList?.Clear();
        activeBarterVM.OfferLbl = "Accept";
        activeBarterVM.IsOfferDisabled = LocalAccepted;
        RefreshResetButton();

        foreach (var item in GetAllBarterItems(activeBarterVM))
            item.IsItemTransferrable = CanOffer(item.Barterable);
    }

    private static void RefreshResetButton()
    {
        if (resetButton == null) return;

        resetButton.IsDisabled = IsActive;
    }

    private static bool IsLocalOfferMessage(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        if (LocalParty == null) return false;
        if (!objectManager.TryGetId(LocalParty, out var localPartyId)) return false;

        return localPartyId == message.PartyId;
    }

    private static void ApplyItemOffers(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        var offeredItems = BuildOfferedItems(message.OfferedItems);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId, objectManager)) continue;
            if (!(item.Barterable is ItemBarterable itemBarterable)) continue;
            if (!TryGetItemKey(itemBarterable.ItemRosterElement, objectManager, out var itemKey)) continue;

            offeredItems.TryGetValue(itemKey, out var offeredAmount);
            ApplyOfferedAmount(item, offeredAmount);
        }
    }

    private static void ApplyGoldOffer(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId, objectManager)) continue;
            if (!(item.Barterable is GoldBarterable)) continue;

            ApplyOfferedAmount(item, message.OfferedGold);
        }
    }

    private static void ApplyFiefOffers(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        var offeredFiefs = new HashSet<string>(message.OfferedFiefs ?? new string[0]);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId, objectManager)) continue;
            if (!(item.Barterable is FiefBarterable fiefBarterable)) continue;
            if (!TryGetSettlementKey(fiefBarterable.TargetSettlement, objectManager, out var fiefKey)) continue;

            ApplyOfferedAmount(item, offeredFiefs.Contains(fiefKey) ? 1 : 0);
        }
    }

    private static void ApplyPrisonerOffers(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        var offeredPrisoners = BuildOfferedTroops(message.OfferedPrisoners);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId, objectManager)) continue;
            if (!(item.Barterable is TransferPrisonerBarterable transferPrisonerBarterable)) continue;
            if (!(PrisonerCharacterField?.GetValue(transferPrisonerBarterable) is Hero prisonerHero)) continue;
            if (!TryGetCharacterKey(prisonerHero.CharacterObject, objectManager, out var prisonerKey)) continue;

            ApplyOfferedAmount(item, offeredPrisoners.ContainsKey(prisonerKey) ? 1 : 0);
        }
    }

    private static void ApplyTroopOffers(NetworkPlayerPartyTradeOfferUpdated message, IObjectManager objectManager)
    {
        var offeredTroops = BuildOfferedTroops(message.OfferedTroops);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId, objectManager)) continue;
            if (!(item.Barterable is PlayerPartyTroopBarterable troopBarterable)) continue;
            if (!TryGetCharacterKey(troopBarterable.TroopRosterElement.Character, objectManager, out var troopKey)) continue;

            offeredTroops.TryGetValue(troopKey, out var offeredAmount);
            ApplyOfferedAmount(item, offeredAmount);
        }
    }

    private static Dictionary<string, int> BuildOfferedItems(ItemRosterElementData[] offeredItems)
    {
        var result = new Dictionary<string, int>();
        if (offeredItems == null) return result;

        foreach (var offeredItem in offeredItems)
        {
            var key = GetItemKey(offeredItem.ItemObjectData);
            if (result.TryGetValue(key, out var currentAmount))
                result[key] = currentAmount + offeredItem.Amount;
            else
                result[key] = offeredItem.Amount;
        }

        return result;
    }

    private static Dictionary<string, int> BuildOfferedTroops(TroopRosterElementData[] offeredTroops)
    {
        var result = new Dictionary<string, int>();
        if (offeredTroops == null) return result;

        foreach (var offeredTroop in offeredTroops)
        {
            var key = GetCharacterKey(offeredTroop.CharacterId, offeredTroop.IsHero);
            if (result.TryGetValue(key, out var currentAmount))
                result[key] = currentAmount + offeredTroop.Number;
            else
                result[key] = offeredTroop.Number;
        }

        return result;
    }

    private static void ApplyOfferedAmount(BarterItemVM item, int offeredAmount)
    {
        var amount = Math.Max(0, Math.Min(offeredAmount, item.TotalItemCount));
        var offerList = GetOfferListForItem(activeBarterVM, item);

        if (amount > 0)
        {
            item.Barterable.SetIsOffered(true);
            item.IsOffered = true;
            item.CurrentOfferedAmount = amount;
            item.IsItemTransferrable = CanOffer(item.Barterable);

            if (!offerList.Contains(item))
                offerList.Add(item);

            return;
        }

        item.Barterable.SetIsOffered(false);
        item.IsOffered = false;
        if (item.IsMultiple)
            item.CurrentOfferedAmount = 1;
        item.IsItemTransferrable = CanOffer(item.Barterable);

        activeBarterVM.LeftOfferList.Remove(item);
        activeBarterVM.RightOfferList.Remove(item);
    }

    private static IEnumerable<BarterItemVM> GetAllBarterItems(BarterVM barterVM)
    {
        scratchItems.Clear();

        AddItems(barterVM.LeftFiefList);
        AddItems(barterVM.RightFiefList);
        AddItems(barterVM.LeftPrisonerList);
        AddItems(barterVM.RightPrisonerList);
        AddItems(barterVM.LeftItemList);
        AddItems(barterVM.RightItemList);
        AddItems(barterVM.LeftOtherList);
        AddItems(barterVM.RightOtherList);
        AddItems(barterVM.LeftDiplomaticList);
        AddItems(barterVM.RightDiplomaticList);
        AddItems(barterVM.LeftGoldList);
        AddItems(barterVM.RightGoldList);
        AddItems(barterVM.LeftOfferList);
        AddItems(barterVM.RightOfferList);

        return scratchItems.ToArray();
    }

    private static void AddItems(IEnumerable<BarterItemVM> items)
    {
        if (items == null) return;

        foreach (var item in items)
        {
            if (item != null)
                scratchItems.Add(item);
        }
    }

    private static bool IsOwnedByParty(BarterItemVM item, string partyId, IObjectManager objectManager)
    {
        if (!TryGetBarterablePartyId(item?.Barterable, objectManager, out var originalPartyId)) return false;

        return originalPartyId == partyId;
    }

    private static bool IsOwnedByLocalParty(FiefBarterable fiefBarterable)
    {
        if (fiefBarterable == null || LocalParty == null) return false;
        if (fiefBarterable.OriginalOwner == LocalParty.LeaderHero) return true;

        return fiefBarterable.TargetSettlement?.OwnerClan?.Leader == LocalParty.LeaderHero;
    }

    private static bool TryGetBarterablePartyId(Barterable barterable, IObjectManager objectManager, out string partyId)
    {
        partyId = null;

        if (barterable == null || objectManager == null) return false;

        var party = barterable.OriginalParty;
        if (party == null && barterable is FiefBarterable fiefBarterable)
            party = GetPartyForFiefBarterable(fiefBarterable);

        return party != null && objectManager.TryGetId(party, out partyId);
    }

    private static PartyBase GetPartyForFiefBarterable(FiefBarterable fiefBarterable)
    {
        try
        {
            return fiefBarterable.OriginalOwner?.PartyBelongedTo?.Party ??
                   fiefBarterable.TargetSettlement?.OwnerClan?.Leader?.PartyBelongedTo?.Party;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static bool TryGetItemKey(ItemRosterElement item, IObjectManager objectManager, out string key)
    {
        key = null;

        if (item.EquipmentElement.Item == null) return false;
        if (!objectManager.TryGetId(item.EquipmentElement.Item, out var itemObjectId)) return false;

        string itemModifierId = null;
        var itemModifierNull = item.EquipmentElement.ItemModifier == null;
        if (!itemModifierNull && !objectManager.TryGetId(item.EquipmentElement.ItemModifier, out itemModifierId))
            return false;

        key = GetItemKey(new ItemObjectData(itemObjectId, itemModifierId, itemModifierNull));
        return true;
    }

    private static bool TryGetSettlementKey(Settlement settlement, IObjectManager objectManager, out string key)
    {
        key = null;

        if (settlement == null) return false;

        key = settlement.StringId;
        return !string.IsNullOrEmpty(key);
    }

    private static bool TryGetCharacterKey(CharacterObject character, IObjectManager objectManager, out string key)
    {
        key = null;

        if (character == null) return false;

        var hero = character.HeroObject;
        var isHero = hero != null;
        string characterId;
        if (isHero)
        {
            if (!objectManager.TryGetId(hero, out characterId)) return false;
        }
        else if (!objectManager.TryGetId(character, out characterId)) return false;

        key = GetCharacterKey(characterId, isHero);
        return true;
    }

    private static string GetItemKey(ItemObjectData itemObjectData)
        => $"{itemObjectData.ItemObjectId}|{itemObjectData.ItemModifierId}|{itemObjectData.ItemModifierNull}";

    private static string GetCharacterKey(string characterId, bool isHero)
        => $"{characterId}|{isHero}";

    private static TaleWorlds.Library.MBBindingList<BarterItemVM> GetOfferListForItem(BarterVM barterVM, BarterItemVM item)
    {
        if (Contains(barterVM.LeftFiefList, item) ||
            Contains(barterVM.LeftPrisonerList, item) ||
            Contains(barterVM.LeftItemList, item) ||
            Contains(barterVM.LeftOtherList, item) ||
            Contains(barterVM.LeftDiplomaticList, item) ||
            Contains(barterVM.LeftGoldList, item))
            return barterVM.LeftOfferList;

        return barterVM.RightOfferList;
    }

    private static bool Contains(IEnumerable<BarterItemVM> items, BarterItemVM item)
        => items != null && items.Contains(item);

    private static bool CanOfferAny(IEnumerable<BarterItemVM> items)
        => items != null && items.Any(item => CanOffer(item?.Barterable));

    private static void ShowOutcomeMessage(PlayerPartyInteractionOutcomeType outcomeType)
    {
        var message = GetOutcomeMessage(outcomeType);
        if (message == null) return;

        InformationManager.DisplayMessage(new InformationMessage(message));
    }

    internal static string GetOutcomeMessage(PlayerPartyInteractionOutcomeType outcomeType)
    {
        switch (outcomeType)
        {
            case PlayerPartyInteractionOutcomeType.TradeAccepted:
                return "Barter offer accepted.";
            case PlayerPartyInteractionOutcomeType.TradeDeclined:
                return "Trade proposal declined.";
            case PlayerPartyInteractionOutcomeType.ClanJoinAccepted:
                return "Clan service proposal accepted.";
            case PlayerPartyInteractionOutcomeType.ClanJoinDeclined:
                return "Clan service proposal declined.";
            case PlayerPartyInteractionOutcomeType.VassalAccepted:
                return "Vassalage offer accepted.";
            case PlayerPartyInteractionOutcomeType.VassalDeclined:
                return "Vassalage offer declined.";
            default:
                return null;
        }
    }
}
