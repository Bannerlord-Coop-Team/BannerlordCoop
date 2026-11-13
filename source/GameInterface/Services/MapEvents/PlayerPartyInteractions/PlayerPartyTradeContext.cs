using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Holds this client's player-party trade session and keeps the barter screen in step with it.
internal interface IPlayerPartyTradeContext
{
    // The active trade's session id, or null when no player trade is open.
    string SessionId { get; }

    // True while a player trade is open on this client.
    bool IsActive { get; }

    // The local party's network id, which its offers are sent under.
    string LocalPartyId { get; }

    // Whether this player accepted the current offers.
    bool LocalAccepted { get; }

    // Whether the other player accepted the current offers.
    bool RemoteAccepted { get; }

    // True while ending the trade closes the barter screen, so its native close messages are hidden.
    bool SuppressNativeCloseMessages { get; }

    // Starts a trade session for the local party with nothing accepted.
    void Begin(string sessionId, PartyBase localParty, string localPartyId);

    // Ends the trade and closes its barter screen, unless another session is the active one.
    void End(string sessionId, PlayerPartyInteractionOutcomeType outcomeType);

    // Takes over a barter screen opened for the active trade.
    void SetBarterVM(BarterVM barterVM);

    // Maps the marriage group onto the Diplomatic columns of the barter screen.
    void AddMarriageGroupLists(BarterVM barterVM, BarterData barterData);

    // Applies both players' acceptance from the server.
    void UpdateAcceptance(bool localAccepted, bool remoteAccepted);

    // Whether the local player may move this barterable on or off the offer.
    bool CanOffer(Barterable barterable);

    // Whether a barter item's offered amount may change, which a server offer always may.
    bool CanSetOfferedAmount(Barterable barterable, int currentAmount, int newAmount);

    // Whether the local player may accept the current offers.
    bool CanAccept();

    // Whether the local player may leave the trade.
    bool CanCancel();

    // Whether the barter screen's reset button may run.
    bool CanReset();

    // Whether the local player may change the offer at all.
    bool CanModifyOffer();

    // Tracks the barter screen's reset button so it stays disabled during a trade.
    void SetResetButton(ButtonWidget buttonWidget);

    // Whether a transfer-all button moves anything the local player may offer.
    bool CanOfferTransferAll(BarterVM barterVM, string methodName);

    // Accepts the current offers for the local player.
    void PublishAccept(bool accepted);

    // Leaves the trade for the local player.
    void PublishLeave();

    // Applies an offer the server relayed for this trade to the barter screen.
    void ApplyOfferUpdate(NetworkPlayerPartyTradeOfferUpdated message);

    // Refreshes the barter screen's labels, buttons and which items may move.
    void RefreshBarterControls();

    // Clears both acceptances, which every changed offer does.
    void ResetAcceptance();

    // The barterables the local party currently offers on the barter screen.
    IReadOnlyCollection<Barterable> GetLocalOffer();
}

// Scoped per client, so each client's barter patches and trade handler share only that client's trade.
internal class PlayerPartyTradeContext : IPlayerPartyTradeContext
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public string SessionId { get; private set; }
    public bool IsActive => SessionId != null;
    public string LocalPartyId { get; private set; }
    public bool LocalAccepted { get; private set; }
    public bool RemoteAccepted { get; private set; }
    public bool SuppressNativeCloseMessages { get; private set; }

    private PartyBase localParty;
    private BarterVM activeBarterVM;
    private ButtonWidget resetButton;
    private bool isApplyingServerOffer;

    private readonly HashSet<BarterItemVM> scratchItems = new HashSet<BarterItemVM>();
    private readonly List<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems =
        new List<(BarterItemVM Item, MBBindingList<BarterItemVM> List)>();

    public PlayerPartyTradeContext(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
    }

    public void Begin(string sessionId, PartyBase localParty, string localPartyId)
    {
        SessionId = sessionId;
        this.localParty = localParty;
        LocalPartyId = localPartyId;
        LocalAccepted = false;
        RemoteAccepted = false;
        activeBarterVM = null;
        resetButton = null;
        hiddenMarriageItems.Clear();
    }

    public void End(string sessionId, PlayerPartyInteractionOutcomeType outcomeType)
    {
        if (sessionId != null && SessionId != null && SessionId != sessionId) return;

        var barterVM = activeBarterVM;

        SessionId = null;
        localParty = null;
        LocalPartyId = null;
        LocalAccepted = false;
        RemoteAccepted = false;
        activeBarterVM = null;
        resetButton = null;
        hiddenMarriageItems.Clear();

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

    public void SetBarterVM(BarterVM barterVM)
    {
        if (!IsActive) return;

        activeBarterVM = barterVM;
        UseMarriageItemVMs(barterVM.LeftDiplomaticList);
        UseMarriageItemVMs(barterVM.RightDiplomaticList);
        RefreshBarterControls();
    }

    // Vanilla never maps a group to the Diplomatic column, so marriages take it over as their own category.
    public void AddMarriageGroupLists(BarterVM barterVM, BarterData barterData)
    {
        var group = barterData?.GetBarterGroup<PlayerPartyMarriageBarterGroup>();
        if (group == null) return;

        barterVM._leftList[group] = barterVM.LeftDiplomaticList;
        barterVM._rightList[group] = barterVM.RightDiplomaticList;
    }

    // Runs from the BarterVM constructor postfix, before the barter movie binds to these lists.
    private void UseMarriageItemVMs(MBBindingList<BarterItemVM> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Barterable is PlayerPartyMarriageBarterable marriage)
                items[i] = new PlayerPartyMarriageBarterItemVM(marriage, activeBarterVM.TransferItem, activeBarterVM.OnOfferedAmountChange);
        }
    }

    public void UpdateAcceptance(bool localAccepted, bool remoteAccepted)
    {
        if (!IsActive) return;

        LocalAccepted = localAccepted;
        RemoteAccepted = remoteAccepted;
        RefreshBarterControls();
    }

    public bool CanOffer(Barterable barterable)
    {
        if (!IsActive) return true;
        if (!CanModifyOffer()) return false;
        if (barterable == null || localParty == null) return false;
        if (hiddenMarriageItems.Any(hidden => hidden.Item.Barterable == barterable)) return false;
        if (barterable is FiefBarterable fiefBarterable)
            return IsOwnedByLocalParty(fiefBarterable);

        return barterable.OriginalParty == localParty;
    }

    public bool CanSetOfferedAmount(Barterable barterable, int currentAmount, int newAmount)
    {
        if (!IsActive || isApplyingServerOffer) return true;
        if (currentAmount == 0 && newAmount == 1) return true;

        return CanOffer(barterable);
    }

    public bool CanAccept()
        => !IsActive || !LocalAccepted;

    public bool CanCancel()
        => !IsActive || !LocalAccepted;

    public bool CanReset()
        => !IsActive;

    // Offer changes are allowed after acceptance because every valid change clears both confirmations.
    public bool CanModifyOffer()
        => true;

    public void SetResetButton(ButtonWidget buttonWidget)
    {
        if (!IsActive) return;

        resetButton = buttonWidget;
        RefreshResetButton();
    }

    public bool CanOfferTransferAll(BarterVM barterVM, string methodName)
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

    public void PublishAccept(bool accepted)
    {
        if (!IsActive) return;
        if (!CanAccept()) return;

        if (accepted)
        {
            LocalAccepted = true;
            RefreshBarterControls();
        }

        messageBroker.Publish(this, new PlayerPartyTradeAcceptSelected(SessionId, accepted));
    }

    public void PublishLeave()
    {
        if (!IsActive) return;
        if (!CanCancel()) return;

        messageBroker.Publish(
            this,
            new PlayerPartyInteractionOptionSelected(
                SessionId,
                PlayerPartyInteractionDialogState.PartyId,
                PlayerPartyInteractionOption.Leave));
    }

    public void ApplyOfferUpdate(NetworkPlayerPartyTradeOfferUpdated message)
    {
        if (!IsActive || SessionId != message.SessionId) return;

        // The server clears both acceptances on every offer it relays, the local party's echo included.
        ResetAcceptance();
        if (activeBarterVM == null) return;

        // The local party's own offer is already on its screen.
        if (message.PartyId == LocalPartyId)
        {
            RefreshBarterControls();
            return;
        }

        isApplyingServerOffer = true;

        try
        {
            ApplyItemOffers(message);
            ApplyGoldOffer(message);
            ApplyFiefOffers(message);
            ApplyPrisonerOffers(message);
            ApplyTroopOffers(message);
            ApplyPeaceOffer(message);
            ApplyMarriageOffers(message);
            RefreshBarterControls();
        }
        finally
        {
            isApplyingServerOffer = false;
        }
    }

    // Reads both offer columns because the local party can be on either side, keeping what it may offer.
    public IReadOnlyCollection<Barterable> GetLocalOffer()
    {
        if (activeBarterVM == null) return Array.Empty<Barterable>();

        return activeBarterVM.LeftOfferList
            .Concat(activeBarterVM.RightOfferList)
            .Select(item => item.Barterable)
            .Where(CanOffer)
            .ToArray();
    }

    public void RefreshBarterControls()
    {
        if (activeBarterVM == null) return;

        activeBarterVM.DiplomaticLbl = "Marriages";
        activeBarterVM.OtherLbl = "Misc";
        activeBarterVM.OfferLbl = "Accept";
        activeBarterVM.IsOfferDisabled = LocalAccepted;
        RefreshResetButton();
        RefreshHiddenMarriages();

        foreach (var item in GetAllBarterItems(activeBarterVM))
            item.IsItemTransferrable = CanOffer(item.Barterable);
    }

    // Hides marriages that share a hero with an offered marriage until that marriage is taken off the offer.
    private void RefreshHiddenMarriages()
    {
        var offeredHeroes = new HashSet<Hero>(activeBarterVM.LeftOfferList
            .Concat(activeBarterVM.RightOfferList)
            .Select(item => item.Barterable)
            .OfType<PlayerPartyMarriageBarterable>()
            .SelectMany(marriage => new[] { marriage.HeroBeingMarried, marriage.Spouse }));

        foreach (var hidden in hiddenMarriageItems.ToArray())
        {
            if (IsBlockedMarriage(hidden.Item, offeredHeroes)) continue;

            hiddenMarriageItems.Remove(hidden);
            hidden.List.Add(hidden.Item);
        }

        HideBlockedMarriages(activeBarterVM.LeftDiplomaticList, offeredHeroes);
        HideBlockedMarriages(activeBarterVM.RightDiplomaticList, offeredHeroes);
    }

    private void HideBlockedMarriages(MBBindingList<BarterItemVM> items, HashSet<Hero> offeredHeroes)
    {
        foreach (var item in items.Where(item => IsBlockedMarriage(item, offeredHeroes)).ToArray())
        {
            items.Remove(item);
            hiddenMarriageItems.Add((item, items));
        }
    }

    private static bool IsBlockedMarriage(BarterItemVM item, HashSet<Hero> offeredHeroes)
        => !item.IsOffered && item.Barterable is PlayerPartyMarriageBarterable marriage &&
           (offeredHeroes.Contains(marriage.HeroBeingMarried) || offeredHeroes.Contains(marriage.Spouse));

    private void RefreshResetButton()
    {
        if (resetButton == null) return;

        resetButton.IsDisabled = IsActive;
    }

    public void ResetAcceptance()
    {
        if (!LocalAccepted && !RemoteAccepted) return;

        LocalAccepted = false;
        RemoteAccepted = false;
        RefreshBarterControls();
        PlayerPartyTradeOverlay.Instance.UpdateState(false, false);
    }

    private void ApplyItemOffers(NetworkPlayerPartyTradeOfferUpdated message)
    {
        var offeredItems = BuildOfferedItems(message.OfferedItems);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is ItemBarterable itemBarterable)) continue;
            if (!TryGetItemKey(itemBarterable.ItemRosterElement, out var itemKey)) continue;

            offeredItems.TryGetValue(itemKey, out var offeredAmount);
            ApplyOfferedAmount(item, offeredAmount);
        }
    }

    private void ApplyGoldOffer(NetworkPlayerPartyTradeOfferUpdated message)
    {
        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is GoldBarterable)) continue;

            ApplyOfferedAmount(item, message.OfferedGold);
        }
    }

    private void ApplyFiefOffers(NetworkPlayerPartyTradeOfferUpdated message)
    {
        var offeredFiefs = new HashSet<string>(message.OfferedFiefs ?? new string[0]);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is FiefBarterable fiefBarterable)) continue;
            if (!TryGetSettlementKey(fiefBarterable.TargetSettlement, out var fiefKey)) continue;

            ApplyOfferedAmount(item, offeredFiefs.Contains(fiefKey) ? 1 : 0);
        }
    }

    private void ApplyPrisonerOffers(NetworkPlayerPartyTradeOfferUpdated message)
    {
        var offeredPrisoners = BuildOfferedTroops(message.OfferedPrisoners);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is TransferPrisonerBarterable transferPrisonerBarterable)) continue;
            if (!TryGetCharacterKey(transferPrisonerBarterable._prisonerCharacter?.CharacterObject, out var prisonerKey)) continue;

            ApplyOfferedAmount(item, offeredPrisoners.ContainsKey(prisonerKey) ? 1 : 0);
        }
    }

    private void ApplyTroopOffers(NetworkPlayerPartyTradeOfferUpdated message)
    {
        var offeredTroops = BuildOfferedTroops(message.OfferedTroops);

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is PlayerPartyTroopBarterable troopBarterable)) continue;
            if (!TryGetCharacterKey(troopBarterable.TroopRosterElement.Character, out var troopKey)) continue;

            offeredTroops.TryGetValue(troopKey, out var offeredAmount);
            ApplyOfferedAmount(item, offeredAmount);
        }
    }

    private void ApplyPeaceOffer(NetworkPlayerPartyTradeOfferUpdated message)
    {
        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is PlayerPartyPeaceBarterable)) continue;

            ApplyOfferedAmount(item, message.OfferedPeace ? 1 : 0);
        }
    }

    private void ApplyMarriageOffers(NetworkPlayerPartyTradeOfferUpdated message)
    {
        var offeredMarriages = new HashSet<string>(
            (message.OfferedMarriages ?? Array.Empty<PlayerPartyMarriageOfferData>())
                .Select(offer => $"{offer.HeroId}|{offer.SpouseId}"));

        foreach (var item in GetAllBarterItems(activeBarterVM))
        {
            if (!IsOwnedByParty(item, message.PartyId)) continue;
            if (!(item.Barterable is PlayerPartyMarriageBarterable marriageBarterable)) continue;
            if (!objectManager.TryGetId(marriageBarterable.HeroBeingMarried, out var heroId)) continue;
            if (!objectManager.TryGetId(marriageBarterable.Spouse, out var spouseId)) continue;

            ApplyOfferedAmount(item, offeredMarriages.Contains($"{heroId}|{spouseId}") ? 1 : 0);
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

    private static Dictionary<uint, int> BuildOfferedTroops(TroopRosterElementData[] offeredTroops)
    {
        var result = new Dictionary<uint, int>();
        if (offeredTroops == null) return result;

        foreach (var offeredTroop in offeredTroops)
        {
            if (result.TryGetValue(offeredTroop.CharacterId, out var currentAmount))
                result[offeredTroop.CharacterId] = currentAmount + offeredTroop.Number;
            else
                result[offeredTroop.CharacterId] = offeredTroop.Number;
        }

        return result;
    }

    private void ApplyOfferedAmount(BarterItemVM item, int offeredAmount)
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

    private IEnumerable<BarterItemVM> GetAllBarterItems(BarterVM barterVM)
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
        AddItems(hiddenMarriageItems.Select(hidden => hidden.Item));

        return scratchItems.ToArray();
    }

    private void AddItems(IEnumerable<BarterItemVM> items)
    {
        if (items == null) return;

        foreach (var item in items)
        {
            if (item != null)
                scratchItems.Add(item);
        }
    }

    private bool IsOwnedByParty(BarterItemVM item, string partyId)
    {
        if (!TryGetBarterablePartyId(item?.Barterable, out var originalPartyId)) return false;

        return originalPartyId == partyId;
    }

    private bool IsOwnedByLocalParty(FiefBarterable fiefBarterable)
    {
        if (fiefBarterable == null || localParty == null) return false;
        if (fiefBarterable.OriginalOwner == localParty.LeaderHero) return true;

        return fiefBarterable.TargetSettlement?.OwnerClan?.Leader == localParty.LeaderHero;
    }

    private bool TryGetBarterablePartyId(Barterable barterable, out string partyId)
    {
        partyId = null;

        if (barterable == null) return false;

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

    private bool TryGetItemKey(ItemRosterElement item, out string key)
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

    private static bool TryGetSettlementKey(Settlement settlement, out string key)
    {
        key = null;

        if (settlement == null) return false;

        key = settlement.StringId;
        return !string.IsNullOrEmpty(key);
    }

    private bool TryGetCharacterKey(CharacterObject character, out uint key)
    {
        key = 0;

        if (character == null) return false;

        return objectManager.TryGetHandle(character, out key);
    }

    private static string GetItemKey(ItemObjectData itemObjectData)
        => $"{itemObjectData.ItemObjectId}|{itemObjectData.ItemModifierId}|{itemObjectData.ItemModifierNull}";

    private TaleWorlds.Library.MBBindingList<BarterItemVM> GetOfferListForItem(BarterVM barterVM, BarterItemVM item)
    {
        if (Contains(barterVM.LeftFiefList, item) ||
            Contains(barterVM.LeftPrisonerList, item) ||
            Contains(barterVM.LeftItemList, item) ||
            Contains(barterVM.LeftOtherList, item) ||
            Contains(barterVM.LeftDiplomaticList, item) ||
            Contains(barterVM.LeftGoldList, item) ||
            hiddenMarriageItems.Contains((item, barterVM.LeftDiplomaticList)))
            return barterVM.LeftOfferList;

        return barterVM.RightOfferList;
    }

    private static bool Contains(IEnumerable<BarterItemVM> items, BarterItemVM item)
        => items != null && items.Contains(item);

    private bool CanOfferAny(IEnumerable<BarterItemVM> items)
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
            case PlayerPartyInteractionOutcomeType.ProposalWithdrawn:
                return GameTexts.FindText("str_coop_player_interaction_withdrawn").ToString();
            case PlayerPartyInteractionOutcomeType.ClanJoinAccepted:
                return "Clan service proposal accepted.";
            case PlayerPartyInteractionOutcomeType.ClanJoinDeclined:
                return "Clan service proposal declined.";
            case PlayerPartyInteractionOutcomeType.MarriageAccepted:
                return GameTexts.FindText("str_coop_marriage_accepted").ToString();
            case PlayerPartyInteractionOutcomeType.MarriageDeclined:
                return GameTexts.FindText("str_coop_marriage_declined").ToString();
            case PlayerPartyInteractionOutcomeType.VassalAccepted:
                return "Vassalage offer accepted.";
            case PlayerPartyInteractionOutcomeType.VassalDeclined:
                return "Vassalage offer declined.";
            case PlayerPartyInteractionOutcomeType.MercenaryAccepted:
                return "Mercenary offer accepted.";
            case PlayerPartyInteractionOutcomeType.MercenaryDeclined:
                return "Mercenary offer declined.";
            case PlayerPartyInteractionOutcomeType.MapTrackingAccepted:
                return GameTexts.FindText("str_coop_map_tracking_accepted").ToString();
            case PlayerPartyInteractionOutcomeType.MapTrackingDeclined:
                return GameTexts.FindText("str_coop_map_tracking_declined").ToString();
            case PlayerPartyInteractionOutcomeType.MapTrackingStopped:
                return GameTexts.FindText("str_coop_map_tracking_stopped").ToString();
            default:
                return null;
        }
    }
}
