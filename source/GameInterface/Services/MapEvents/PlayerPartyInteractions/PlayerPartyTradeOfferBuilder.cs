using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Builds the local party's trade offer message from the barterables it offers.
internal interface IPlayerPartyTradeOfferBuilder
{
    // Builds the offer from barterables already filtered to what the local party may offer.
    NetworkPlayerPartyTradeOfferUpdated Build(string sessionId, string partyId, IReadOnlyCollection<Barterable> offered);
}

// Resolves the local party's offered barterables into the network keys of the offer message.
internal class PlayerPartyTradeOfferBuilder : IPlayerPartyTradeOfferBuilder
{
    private readonly IPlayerPartyTradeKeyResolver keyResolver;

    public PlayerPartyTradeOfferBuilder(IPlayerPartyTradeKeyResolver keyResolver)
    {
        this.keyResolver = keyResolver;
    }

    // Translates the offered barterables into the offer message, each by the key every instance shares.
    public NetworkPlayerPartyTradeOfferUpdated Build(string sessionId, string partyId, IReadOnlyCollection<Barterable> offered)
    {
        var offeredGold = offered.OfType<GoldBarterable>().Sum(gold => Math.Max(0, gold.CurrentAmount));
        var offeredPeace = offered.OfType<PlayerPartyPeaceBarterable>().Any();

        return new NetworkPlayerPartyTradeOfferUpdated(
            sessionId: sessionId,
            partyId: partyId,
            offeredItems: ResolveItems(offered.OfType<ItemBarterable>()),
            offeredTroops: ResolveTroops(offered.OfType<PlayerPartyTroopBarterable>()),
            offeredGold: offeredGold,
            offeredFiefs: ResolveFiefs(offered.OfType<FiefBarterable>()),
            offeredPrisoners: ResolvePrisoners(offered.OfType<TransferPrisonerBarterable>()),
            offeredPeace: offeredPeace,
            offeredMarriages: ResolveMarriages(offered.OfType<PlayerPartyMarriageBarterable>()));
    }

    // Resolves offered item stacks, capped at what the roster holds.
    private ItemRosterElementData[] ResolveItems(IEnumerable<ItemBarterable> items)
    {
        var result = new List<ItemRosterElementData>();

        foreach (var item in items)
        {
            var count = Math.Min(item.CurrentAmount, item.ItemRosterElement.Amount);
            if (count <= 0) continue;
            if (!keyResolver.TryResolveItem(item, out var itemKey)) continue;

            result.Add(new ItemRosterElementData(itemKey, count));
        }

        return result.ToArray();
    }

    // Resolves offered troop stacks, capped at what the roster holds.
    private TroopRosterElementData[] ResolveTroops(IEnumerable<PlayerPartyTroopBarterable> troops)
    {
        var result = new List<TroopRosterElementData>();

        foreach (var troop in troops)
        {
            var element = troop.TroopRosterElement;
            var count = Math.Min(troop.CurrentAmount, element.Number);
            if (count <= 0) continue;
            if (!keyResolver.TryResolveTroop(troop, out var characterId)) continue;

            result.Add(new TroopRosterElementData(characterId, count, element.WoundedNumber, element.Xp));
        }

        return result.ToArray();
    }

    // Resolves offered fiefs by their settlements.
    private string[] ResolveFiefs(IEnumerable<FiefBarterable> fiefs)
    {
        var result = new List<string>();

        foreach (var fief in fiefs)
        {
            if (!keyResolver.TryResolveFief(fief, out var settlementId)) continue;

            result.Add(settlementId);
        }

        return result.ToArray();
    }

    // Resolves offered prisoners, one of each.
    private TroopRosterElementData[] ResolvePrisoners(IEnumerable<TransferPrisonerBarterable> prisoners)
    {
        var result = new List<TroopRosterElementData>();

        foreach (var prisoner in prisoners)
        {
            if (!keyResolver.TryResolvePrisoner(prisoner, out var characterId)) continue;

            result.Add(new TroopRosterElementData(characterId, number: 1, woundedNumber: 0, xp: 0));
        }

        return result.ToArray();
    }

    // Resolves offered marriages.
    private PlayerPartyMarriageOfferData[] ResolveMarriages(IEnumerable<PlayerPartyMarriageBarterable> marriages)
    {
        var result = new List<PlayerPartyMarriageOfferData>();

        foreach (var marriage in marriages)
        {
            if (!keyResolver.TryResolveMarriage(marriage, out var marriageKey)) continue;

            result.Add(marriageKey);
        }

        return result.ToArray();
    }
}
