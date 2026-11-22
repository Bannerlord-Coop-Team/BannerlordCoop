using Common.Logging;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
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

    // Starts a trade session for the local party with nothing accepted and shows the trade overlay.
    void Begin(string sessionId, PartyBase localParty, string localPartyId, string otherPlayerName);

    // Ends the trade, closes its barter screen and hides the overlay, unless another session is the active one.
    void End(string sessionId);

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

    // Whether the barter screen's reset button may run.
    bool CanReset();

    // Tracks the barter screen's reset button so it stays disabled during a trade.
    void SetResetButton(ButtonWidget buttonWidget);

    // Whether a transfer-all button moves anything the local player may offer.
    bool CanOfferTransferAll(BarterVM barterVM, string methodName);

    // Marks the local player as having accepted the current offers.
    void SetLocalAccepted();

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
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyTradeContext>();

    private readonly IPlayerPartyTradeOfferResolver offerResolver;
    private readonly IPlayerPartyTradeOverlayInterface tradeOverlay;
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

    private readonly List<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems =
        new List<(BarterItemVM Item, MBBindingList<BarterItemVM> List)>();

    public PlayerPartyTradeContext(
        IPlayerPartyTradeOfferResolver offerResolver,
        IPlayerPartyTradeOverlayInterface tradeOverlay,
        IObjectManager objectManager)
    {
        this.offerResolver = offerResolver;
        this.tradeOverlay = tradeOverlay;
        this.objectManager = objectManager;
    }

    public void Begin(string sessionId, PartyBase localParty, string localPartyId, string otherPlayerName)
    {
        SessionId = sessionId;
        this.localParty = localParty;
        LocalPartyId = localPartyId;
        LocalAccepted = false;
        RemoteAccepted = false;
        activeBarterVM = null;
        resetButton = null;
        hiddenMarriageItems.Clear();

        try
        {
            tradeOverlay.Show(sessionId, otherPlayerName);
        }
        catch (NullReferenceException ex)
        {
            Logger.Warning(ex, "Unable to open player party trade overlay");
        }
    }

    public void End(string sessionId)
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

        tradeOverlay.Hide(sessionId);
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

        SetAcceptance(localAccepted, remoteAccepted);
    }

    public bool CanOffer(Barterable barterable)
    {
        if (!IsActive) return true;
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

    public bool CanReset()
        => !IsActive;

    public void SetResetButton(ButtonWidget buttonWidget)
    {
        if (!IsActive) return;

        resetButton = buttonWidget;
        RefreshResetButton();
    }

    public bool CanOfferTransferAll(BarterVM barterVM, string methodName)
    {
        if (!IsActive) return true;
        if (barterVM == null) return false;

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

    public void SetLocalAccepted()
        => SetAcceptance(localAccepted: true, RemoteAccepted);

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

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.PartyId, out var offeringParty)) return;

        // The flag lets these writes past the amount-setter patch, which otherwise refuses changes to the other party's items.
        isApplyingServerOffer = true;

        try
        {
            // Back on their lists the offer can reach hidden marriages, and the refresh re-hides the ones that still clash.
            RestoreHiddenMarriages();

            var offeringItems = GetAllBarterItems()
                .Where(item => GetOfferingParty(item.Barterable) == offeringParty)
                .ToArray();

            foreach (var (item, amount) in offerResolver.GetOfferedAmounts(message, offeringItems))
                ApplyOfferedAmount(item, amount);

            RefreshBarterControls();
        }
        finally
        {
            isApplyingServerOffer = false;
        }
    }

    // Puts the item on its side's offer at the offered amount, or takes it off when none is offered.
    private void ApplyOfferedAmount(BarterItemVM item, int offeredAmount)
    {
        var amount = Math.Max(0, Math.Min(offeredAmount, item.TotalItemCount));
        var isOffered = amount > 0;
        item.Barterable.SetIsOffered(isOffered);
        item.IsOffered = isOffered;

        if (!isOffered)
        {
            if (item.IsMultiple)
                item.CurrentOfferedAmount = 1;

            activeBarterVM.LeftOfferList.Remove(item);
            activeBarterVM.RightOfferList.Remove(item);
            return;
        }

        item.CurrentOfferedAmount = amount;

        var offerList = GetOfferListForItem(item);
        if (!offerList.Contains(item))
            offerList.Add(item);
    }

    // An offered item stays on its group list, so that list's side is the side it offers from.
    private MBBindingList<BarterItemVM> GetOfferListForItem(BarterItemVM item)
    {
        var isLeftItem = activeBarterVM._leftList.Values.Any(list => list.Contains(item));

        return isLeftItem ? activeBarterVM.LeftOfferList : activeBarterVM.RightOfferList;
    }

    // Every barter item in vanilla's group and offer lists, marriages included.
    private IEnumerable<BarterItemVM> GetAllBarterItems()
        => activeBarterVM._barterList
            .SelectMany(side => side.Values)
            .Concat(activeBarterVM._offerList)
            .SelectMany(list => list)
            .Distinct();

    // Fiefs can have no original party, so they fall back to the party of the hero who holds them.
    private static PartyBase GetOfferingParty(Barterable barterable)
    {
        if (barterable.OriginalParty != null) return barterable.OriginalParty;
        if (barterable is not FiefBarterable fief) return null;

        return fief.OriginalOwner?.PartyBelongedTo?.Party ??
               fief.TargetSettlement?.OwnerClan?.Leader?.PartyBelongedTo?.Party;
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

        // Hidden marriages are off the screen, and refreshing them first put back the ones that no longer clash.
        foreach (var item in GetAllBarterItems())
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

    // Puts every hidden marriage back on the list it was hidden from.
    private void RestoreHiddenMarriages()
    {
        foreach (var hidden in hiddenMarriageItems)
            hidden.List.Add(hidden.Item);

        hiddenMarriageItems.Clear();
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

        SetAcceptance(localAccepted: false, remoteAccepted: false);
    }

    // Acceptance changes during a trade go through here, so the barter screen and the overlay show the same state.
    private void SetAcceptance(bool localAccepted, bool remoteAccepted)
    {
        LocalAccepted = localAccepted;
        RemoteAccepted = remoteAccepted;
        RefreshBarterControls();
        tradeOverlay.UpdateState(localAccepted, remoteAccepted);
    }

    private bool IsOwnedByLocalParty(FiefBarterable fiefBarterable)
    {
        if (fiefBarterable == null || localParty == null) return false;
        if (fiefBarterable.OriginalOwner == localParty.LeaderHero) return true;

        return fiefBarterable.TargetSettlement?.OwnerClan?.Leader == localParty.LeaderHero;
    }

    private bool CanOfferAny(IEnumerable<BarterItemVM> items)
        => items != null && items.Any(item => CanOffer(item?.Barterable));
}
