using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct CreatedCraftedWeaponInternal : IEvent
{
    public readonly bool IsFreeMode;
    public readonly Hero CraftingHero;
    public readonly TextObject Name;
    public readonly BasicCultureObject CultureObject;
    public readonly WeaponDesign WeaponDesign;
    public readonly ItemModifier WeaponModifier;
    public readonly Hero PlayerHero;
    public readonly Crafting CraftingLogic;
    public readonly CraftingOrder CraftingOrder;
    public readonly Settlement CurrentSettlement;

    public CreatedCraftedWeaponInternal(
        bool isFreeMode,
        Hero craftingHero,
        TextObject name,
        BasicCultureObject cultureObject,
        WeaponDesign weaponDesign,
        ItemModifier weaponModifier,
        Hero playerHero,
        Crafting craftingLogic,
        CraftingOrder craftingOrder,
        Settlement currentSettlement)
    {
        IsFreeMode = isFreeMode;
        CraftingHero = craftingHero;
        Name = name;
        CultureObject = cultureObject;
        WeaponDesign = weaponDesign;
        WeaponModifier = weaponModifier;
        PlayerHero = playerHero;
        CraftingLogic = craftingLogic;
        CraftingOrder = craftingOrder;
        CurrentSettlement = currentSettlement;
    }
}