using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Builds the local party's trade offer message from the barter screen.
internal interface IPlayerPartyTradeOfferBuilder
{
    // Builds the offer from every barterable the local party has put on the barter screen.
    NetworkPlayerPartyTradeOfferUpdated Build(string sessionId, string partyId, BarterVM barterVM);
}

// Resolves the local party's offered barterables into the network ids of the offer message.
internal class PlayerPartyTradeOfferBuilder : IPlayerPartyTradeOfferBuilder
{
    private readonly IObjectManager objectManager;

    public PlayerPartyTradeOfferBuilder(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    // Both offer columns are read because the local party can be on either side of the screen.
    public NetworkPlayerPartyTradeOfferUpdated Build(string sessionId, string partyId, BarterVM barterVM)
    {
        var offered = barterVM.LeftOfferList
            .Concat(barterVM.RightOfferList)
            .Select(item => item.Barterable)
            .Where(PlayerPartyTradeContext.CanOffer)
            .ToArray();

        return new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            partyId,
            ResolveItems(offered.OfType<ItemBarterable>()),
            ResolveTroops(offered.OfType<PlayerPartyTroopBarterable>()),
            offered.OfType<GoldBarterable>().Sum(gold => Math.Max(0, gold.CurrentAmount)),
            ResolveFiefs(offered.OfType<FiefBarterable>()),
            ResolvePrisoners(offered.OfType<TransferPrisonerBarterable>()),
            offered.OfType<PlayerPartyPeaceBarterable>().Any(),
            ResolveMarriages(offered.OfType<PlayerPartyMarriageBarterable>()));
    }

    // Resolves offered item stacks, capped at what the roster holds.
    private ItemRosterElementData[] ResolveItems(IEnumerable<ItemBarterable> items)
    {
        var result = new List<ItemRosterElementData>();

        foreach (var item in items)
        {
            var count = Math.Min(item.CurrentAmount, item.ItemRosterElement.Amount);
            if (count <= 0) continue;

            var equipment = item.ItemRosterElement.EquipmentElement;
            if (!objectManager.TryGetId(equipment.Item, out var itemObjectId)) continue;

            string itemModifierId = null;
            if (equipment.ItemModifier != null && !objectManager.TryGetId(equipment.ItemModifier, out itemModifierId)) continue;

            result.Add(new ItemRosterElementData(new ItemObjectData(itemObjectId, itemModifierId, equipment.ItemModifier == null), count));
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

            result.Add(new TroopRosterElementData(characterId, 1, 0, 0));
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
