using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Applies the other party's trade offer, as the server relayed it, to the local barter screen.
internal interface IPlayerPartyTradeOfferApplier
{
    // Moves the offering party's barter items, hidden marriages included, on or off the offer to match it.
    void Apply(
        NetworkPlayerPartyTradeOfferUpdated offer,
        BarterVM barterVM,
        IReadOnlyCollection<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems);
}

// Resolves an offer's network ids back to the barter items they name, the reverse of the offer builder.
internal class PlayerPartyTradeOfferApplier : IPlayerPartyTradeOfferApplier
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerPartyTradeOfferBuilder offerBuilder;

    public PlayerPartyTradeOfferApplier(IObjectManager objectManager, IPlayerPartyTradeOfferBuilder offerBuilder)
    {
        this.objectManager = objectManager;
        this.offerBuilder = offerBuilder;
    }

    // Leaves which items may move to the caller, which refreshes them after every applied offer.
    public void Apply(
        NetworkPlayerPartyTradeOfferUpdated offer,
        BarterVM barterVM,
        IReadOnlyCollection<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems)
    {
        var offeringItems = GetAllBarterItems(barterVM, hiddenMarriageItems)
            .Where(item => IsOwnedByParty(item.Barterable, offer.PartyId))
            .ToArray();

        var offeredAmounts = GetItemAmounts(offer, offeringItems)
            .Concat(GetGoldAmounts(offer, offeringItems))
            .Concat(GetFiefAmounts(offer, offeringItems))
            .Concat(GetPrisonerAmounts(offer, offeringItems))
            .Concat(GetTroopAmounts(offer, offeringItems))
            .Concat(GetPeaceAmounts(offer, offeringItems))
            .Concat(GetMarriageAmounts(offer, offeringItems));

        foreach (var (item, amount) in offeredAmounts)
            ApplyOfferedAmount(barterVM, hiddenMarriageItems, item, amount);
    }

    // Pairs each item stack with how many of it the offer holds, keyed the way the offer builder sends it.
    private IEnumerable<(BarterItemVM Item, int Amount)> GetItemAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
    {
        var offeredItems = SumByKey(offer.OfferedItems, offered => GetItemKey(offered.ItemObjectData), offered => offered.Amount);

        foreach (var item in items)
        {
            if (!(item.Barterable is ItemBarterable itemBarterable)) continue;
            if (!offerBuilder.TryResolveItem(itemBarterable.ItemRosterElement.EquipmentElement, out var itemData)) continue;

            offeredItems.TryGetValue(GetItemKey(itemData), out var amount);
            yield return (item, amount);
        }
    }

    // Pairs each gold item with the offered gold.
    private static IEnumerable<(BarterItemVM Item, int Amount)> GetGoldAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
        => items
            .Where(item => item.Barterable is GoldBarterable)
            .Select(item => (item, offer.OfferedGold));

    // Pairs each fief with whether the offer holds its settlement.
    private static IEnumerable<(BarterItemVM Item, int Amount)> GetFiefAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
    {
        var offeredFiefs = new HashSet<string>(offer.OfferedFiefs ?? Array.Empty<string>());

        foreach (var item in items)
        {
            if (!(item.Barterable is FiefBarterable fiefBarterable)) continue;

            var settlementId = fiefBarterable.TargetSettlement?.StringId;
            if (string.IsNullOrEmpty(settlementId)) continue;

            yield return (item, offeredFiefs.Contains(settlementId) ? 1 : 0);
        }
    }

    // Pairs each prisoner with whether the offer holds that prisoner's character.
    private IEnumerable<(BarterItemVM Item, int Amount)> GetPrisonerAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
    {
        var offeredPrisoners = new HashSet<uint>((offer.OfferedPrisoners ?? Array.Empty<TroopRosterElementData>()).Select(prisoner => prisoner.CharacterId));

        foreach (var item in items)
        {
            if (!(item.Barterable is TransferPrisonerBarterable prisonerBarterable)) continue;

            var character = prisonerBarterable._prisonerCharacter?.CharacterObject;
            if (character == null) continue;
            if (!objectManager.TryGetHandleWithLogging(character, out var characterId)) continue;

            yield return (item, offeredPrisoners.Contains(characterId) ? 1 : 0);
        }
    }

    // Pairs each troop stack with how many of that troop the offer holds.
    private IEnumerable<(BarterItemVM Item, int Amount)> GetTroopAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
    {
        var offeredTroops = SumByKey(offer.OfferedTroops, troop => troop.CharacterId, troop => troop.Number);

        foreach (var item in items)
        {
            if (!(item.Barterable is PlayerPartyTroopBarterable troopBarterable)) continue;

            var character = troopBarterable.TroopRosterElement.Character;
            if (character == null) continue;
            if (!objectManager.TryGetHandleWithLogging(character, out var characterId)) continue;

            offeredTroops.TryGetValue(characterId, out var amount);
            yield return (item, amount);
        }
    }

    // Pairs the peace item with whether peace is offered.
    private static IEnumerable<(BarterItemVM Item, int Amount)> GetPeaceAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
        => items
            .Where(item => item.Barterable is PlayerPartyPeaceBarterable)
            .Select(item => (item, offer.OfferedPeace ? 1 : 0));

    // Pairs each marriage with whether the offer holds that couple.
    private IEnumerable<(BarterItemVM Item, int Amount)> GetMarriageAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> items)
    {
        var offeredMarriages = new HashSet<(string, string)>(
            (offer.OfferedMarriages ?? Array.Empty<PlayerPartyMarriageOfferData>()).Select(marriage => (marriage.HeroId, marriage.SpouseId)));

        foreach (var item in items)
        {
            if (!(item.Barterable is PlayerPartyMarriageBarterable marriageBarterable)) continue;
            if (!objectManager.TryGetIdWithLogging(marriageBarterable.HeroBeingMarried, out var heroId)) continue;
            if (!objectManager.TryGetIdWithLogging(marriageBarterable.Spouse, out var spouseId)) continue;

            yield return (item, offeredMarriages.Contains((heroId, spouseId)) ? 1 : 0);
        }
    }

    // Totals the amounts per key, because an offer may carry the same stack more than once.
    private static Dictionary<TKey, int> SumByKey<T, TKey>(IEnumerable<T> entries, Func<T, TKey> getKey, Func<T, int> getAmount)
        => (entries ?? Enumerable.Empty<T>())
            .GroupBy(getKey)
            .ToDictionary(group => group.Key, group => group.Sum(getAmount));

    // An item's offer identity, so stacks that differ only by modifier stay apart.
    private static (string, string, bool) GetItemKey(ItemObjectData item)
        => (item.ItemObjectId, item.ItemModifierId, item.ItemModifierNull);

    // Puts the item on its side's offer at the offered amount, or takes it off when none is offered.
    private static void ApplyOfferedAmount(
        BarterVM barterVM,
        IReadOnlyCollection<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems,
        BarterItemVM item,
        int offeredAmount)
    {
        var amount = Math.Max(0, Math.Min(offeredAmount, item.TotalItemCount));

        if (amount > 0)
        {
            item.Barterable.SetIsOffered(true);
            item.IsOffered = true;
            item.CurrentOfferedAmount = amount;

            var offerList = GetOfferListForItem(barterVM, hiddenMarriageItems, item);
            if (!offerList.Contains(item))
                offerList.Add(item);

            return;
        }

        item.Barterable.SetIsOffered(false);
        item.IsOffered = false;
        if (item.IsMultiple)
            item.CurrentOfferedAmount = 1;

        barterVM.LeftOfferList.Remove(item);
        barterVM.RightOfferList.Remove(item);
    }

    // Every barter item in vanilla's group and offer lists, marriages included, plus the marriages hidden from them.
    private static BarterItemVM[] GetAllBarterItems(
        BarterVM barterVM,
        IReadOnlyCollection<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems)
        => barterVM._barterList
            .SelectMany(side => side.Values)
            .Concat(barterVM._offerList)
            .SelectMany(list => list)
            .Concat(hiddenMarriageItems.Select(hidden => hidden.Item))
            .Distinct()
            .ToArray();

    // An item offers from the side its list is on, which for a hidden marriage is the list it was hidden from.
    private static MBBindingList<BarterItemVM> GetOfferListForItem(
        BarterVM barterVM,
        IReadOnlyCollection<(BarterItemVM Item, MBBindingList<BarterItemVM> List)> hiddenMarriageItems,
        BarterItemVM item)
    {
        var isLeft = barterVM._leftList.Values.Any(list => list.Contains(item)) ||
                     hiddenMarriageItems.Contains((item, barterVM.LeftDiplomaticList));

        return isLeft ? barterVM.LeftOfferList : barterVM.RightOfferList;
    }

    // Fiefs can have no original party, so they fall back to the party of the hero who holds them.
    private bool IsOwnedByParty(Barterable barterable, string partyId)
    {
        if (barterable == null) return false;

        var party = barterable.OriginalParty ?? GetPartyForFiefBarterable(barterable as FiefBarterable);
        if (party == null) return false;

        return objectManager.TryGetId(party, out var originalPartyId) && originalPartyId == partyId;
    }

    // The party of the hero holding the fief, or null when there is none.
    private static PartyBase GetPartyForFiefBarterable(FiefBarterable fiefBarterable)
    {
        if (fiefBarterable == null) return null;

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
}
