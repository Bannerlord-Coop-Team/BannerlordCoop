using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Builds the local party's trade offer message from the barterables it offers.
internal interface IPlayerPartyTradeOfferBuilder
{
    // Builds the offer from barterables already filtered to what the local party may offer.
    NetworkPlayerPartyTradeOfferUpdated Build(string sessionId, string partyId, IReadOnlyCollection<Barterable> offered);

    // Resolves an item and its modifier into the ids an offer carries them by.
    bool TryResolveItem(EquipmentElement equipment, out ItemObjectData item);
}

// Resolves the local party's offered barterables into the network ids of the offer message.
internal class PlayerPartyTradeOfferBuilder : IPlayerPartyTradeOfferBuilder
{
    private readonly IObjectManager objectManager;

    public PlayerPartyTradeOfferBuilder(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    // Translates the offered barterables into the offer message, each by the id every instance shares.
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
            if (!TryResolveItem(item.ItemRosterElement.EquipmentElement, out var itemData)) continue;

            result.Add(new ItemRosterElementData(itemData, count));
        }

        return result.ToArray();
    }

    // Shared with applying a received offer, so both directions key an item stack the same way.
    public bool TryResolveItem(EquipmentElement equipment, out ItemObjectData item)
    {
        item = default;

        if (!objectManager.TryGetIdWithLogging(equipment.Item, out var itemObjectId)) return false;

        string itemModifierId = null;
        var hasModifier = equipment.ItemModifier != null;
        if (hasModifier && !objectManager.TryGetIdWithLogging(equipment.ItemModifier, out itemModifierId)) return false;

        item = new ItemObjectData(itemObjectId, itemModifierId, itemModifierNull: !hasModifier);
        return true;
    }

    // Resolves offered troop stacks, capped at what the roster holds.
    private TroopRosterElementData[] ResolveTroops(IEnumerable<PlayerPartyTroopBarterable> troops)
    {
        var result = new List<TroopRosterElementData>();

        foreach (var troop in troops)
        {
            var element = troop.TroopRosterElement;
            var count = Math.Min(troop.CurrentAmount, element.Number);
            if (element.Character == null || count <= 0) continue;
            if (!objectManager.TryGetHandleWithLogging(element.Character, out var characterId)) continue;

            result.Add(new TroopRosterElementData(characterId, count, element.WoundedNumber, element.Xp));
        }

        return result.ToArray();
    }

    // Fiefs go by settlement string id, which every instance shares.
    private static string[] ResolveFiefs(IEnumerable<FiefBarterable> fiefs)
        => fiefs
            .Select(fief => fief.TargetSettlement?.StringId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();

    // Resolves offered prisoners by the prisoner hero's character.
    private TroopRosterElementData[] ResolvePrisoners(IEnumerable<TransferPrisonerBarterable> prisoners)
    {
        var result = new List<TroopRosterElementData>();

        foreach (var prisoner in prisoners)
        {
            var character = prisoner._prisonerCharacter?.CharacterObject;
            if (character == null) continue;
            if (!objectManager.TryGetHandleWithLogging(character, out var characterId)) continue;

            result.Add(new TroopRosterElementData(characterId, number: 1, woundedNumber: 0, xp: 0));
        }

        return result.ToArray();
    }

    // Resolves offered marriages by both heroes' ids.
    private PlayerPartyMarriageOfferData[] ResolveMarriages(IEnumerable<PlayerPartyMarriageBarterable> marriages)
    {
        var result = new List<PlayerPartyMarriageOfferData>();

        foreach (var marriage in marriages)
        {
            if (!objectManager.TryGetIdWithLogging(marriage.HeroBeingMarried, out var heroId)) continue;
            if (!objectManager.TryGetIdWithLogging(marriage.Spouse, out var spouseId)) continue;

            result.Add(new PlayerPartyMarriageOfferData(heroId, spouseId));
        }

        return result.ToArray();
    }
}
