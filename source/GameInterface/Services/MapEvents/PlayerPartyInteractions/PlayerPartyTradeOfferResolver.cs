using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Reads how much of each barter item a trade offer the server relayed holds, the reverse of the offer builder.
internal interface IPlayerPartyTradeOfferResolver
{
    // Pairs each of the offering party's items that the offer can key with its offered amount, zero when the offer leaves it out.
    IEnumerable<(BarterItemVM Item, int Amount)> GetOfferedAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> offeringItems);
}

// Matches an offer's network keys to barter items through the key resolver the offer builder also uses.
internal class PlayerPartyTradeOfferResolver : IPlayerPartyTradeOfferResolver
{
    private readonly IPlayerPartyTradeKeyResolver keyResolver;

    public PlayerPartyTradeOfferResolver(IPlayerPartyTradeKeyResolver keyResolver)
    {
        this.keyResolver = keyResolver;
    }

    // An item whose key cant be resolved is skipped, so the offer leaves it as it is.
    public IEnumerable<(BarterItemVM Item, int Amount)> GetOfferedAmounts(NetworkPlayerPartyTradeOfferUpdated offer, IEnumerable<BarterItemVM> offeringItems)
    {
        var offeredItems = SumByKey(offer.OfferedItems, offered => GetItemKey(offered.ItemObjectData), offered => offered.Amount);
        var offeredTroops = SumByKey(offer.OfferedTroops, troop => troop.CharacterId, troop => troop.Number);
        var offeredPrisoners = new HashSet<uint>((offer.OfferedPrisoners ?? Array.Empty<TroopRosterElementData>()).Select(prisoner => prisoner.CharacterId));
        var offeredFiefs = new HashSet<string>(offer.OfferedFiefs ?? Array.Empty<string>());
        var offeredMarriages = new HashSet<(string, string)>((offer.OfferedMarriages ?? Array.Empty<PlayerPartyMarriageOfferData>()).Select(GetMarriageKey));

        foreach (var item in offeringItems)
        {
            switch (item.Barterable)
            {
                case GoldBarterable:
                    yield return (item, offer.OfferedGold);
                    break;
                case PlayerPartyPeaceBarterable:
                    yield return (item, offer.OfferedPeace ? 1 : 0);
                    break;
                case ItemBarterable itemBarterable when keyResolver.TryResolveItem(itemBarterable, out var itemKey):
                    offeredItems.TryGetValue(GetItemKey(itemKey), out var itemAmount);
                    yield return (item, itemAmount);
                    break;
                case PlayerPartyTroopBarterable troop when keyResolver.TryResolveTroop(troop, out var troopId):
                    offeredTroops.TryGetValue(troopId, out var troopAmount);
                    yield return (item, troopAmount);
                    break;
                case TransferPrisonerBarterable prisoner when keyResolver.TryResolvePrisoner(prisoner, out var prisonerId):
                    yield return (item, offeredPrisoners.Contains(prisonerId) ? 1 : 0);
                    break;
                case FiefBarterable fief when keyResolver.TryResolveFief(fief, out var settlementId):
                    yield return (item, offeredFiefs.Contains(settlementId) ? 1 : 0);
                    break;
                case PlayerPartyMarriageBarterable marriage when keyResolver.TryResolveMarriage(marriage, out var marriageKey):
                    yield return (item, offeredMarriages.Contains(GetMarriageKey(marriageKey)) ? 1 : 0);
                    break;
            }
        }
    }

    // Totals the amounts per key, because an offer may carry the same stack more than once.
    private static Dictionary<TKey, int> SumByKey<T, TKey>(IEnumerable<T> entries, Func<T, TKey> getKey, Func<T, int> getAmount)
        => (entries ?? Enumerable.Empty<T>())
            .GroupBy(getKey)
            .ToDictionary(group => group.Key, group => group.Sum(getAmount));

    // An item stack's identity, compared by value so stacks that differ only by modifier stay apart.
    private static (string, string, bool) GetItemKey(ItemObjectData item)
        => (item.ItemObjectId, item.ItemModifierId, item.ItemModifierNull);

    // A marriage's identity, compared by value.
    private static (string, string) GetMarriageKey(PlayerPartyMarriageOfferData marriage)
        => (marriage.HeroId, marriage.SpouseId);
}
