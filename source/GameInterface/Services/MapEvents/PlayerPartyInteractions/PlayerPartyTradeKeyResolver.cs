using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Resolves each barterable into the network key a player-party trade offer carries it by.
internal interface IPlayerPartyTradeKeyResolver
{
    // An item stack by its item and modifier ids, so stacks that differ only by modifier stay apart.
    bool TryResolveItem(ItemBarterable item, out ItemObjectData key);

    // A troop stack by its character's handle.
    bool TryResolveTroop(PlayerPartyTroopBarterable troop, out uint characterId);

    // A prisoner by the handle of the prisoner hero's character.
    bool TryResolvePrisoner(TransferPrisonerBarterable prisoner, out uint characterId);

    // A fief by its settlement's id.
    bool TryResolveFief(FiefBarterable fief, out string settlementId);

    // A marriage by both heroes' ids.
    bool TryResolveMarriage(PlayerPartyMarriageBarterable marriage, out PlayerPartyMarriageOfferData key);
}

// Shared by building and applying an offer, so both directions key every barterable the same way.
internal class PlayerPartyTradeKeyResolver : IPlayerPartyTradeKeyResolver
{
    private readonly IObjectManager objectManager;

    public PlayerPartyTradeKeyResolver(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryResolveItem(ItemBarterable item, out ItemObjectData key)
    {
        key = default;
        var equipment = item.ItemRosterElement.EquipmentElement;

        if (!objectManager.TryGetIdWithLogging(equipment.Item, out var itemObjectId)) return false;

        string itemModifierId = null;
        var hasModifier = equipment.ItemModifier != null;
        if (hasModifier && !objectManager.TryGetIdWithLogging(equipment.ItemModifier, out itemModifierId)) return false;

        key = new ItemObjectData(itemObjectId, itemModifierId, itemModifierNull: !hasModifier);
        return true;
    }

    public bool TryResolveTroop(PlayerPartyTroopBarterable troop, out uint characterId)
        => objectManager.TryGetHandleWithLogging(troop.TroopRosterElement.Character, out characterId);

    public bool TryResolvePrisoner(TransferPrisonerBarterable prisoner, out uint characterId)
        => objectManager.TryGetHandleWithLogging(prisoner._prisonerCharacter?.CharacterObject, out characterId);

    public bool TryResolveFief(FiefBarterable fief, out string settlementId)
        => objectManager.TryGetIdWithLogging(fief.TargetSettlement, out settlementId);

    public bool TryResolveMarriage(PlayerPartyMarriageBarterable marriage, out PlayerPartyMarriageOfferData key)
    {
        key = default;

        if (!objectManager.TryGetIdWithLogging(marriage.HeroBeingMarried, out var heroId)) return false;
        if (!objectManager.TryGetIdWithLogging(marriage.Spouse, out var spouseId)) return false;

        key = new PlayerPartyMarriageOfferData(heroId, spouseId);
        return true;
    }
}
