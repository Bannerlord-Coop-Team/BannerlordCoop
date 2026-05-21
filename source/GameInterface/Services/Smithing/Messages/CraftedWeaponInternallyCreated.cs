using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public record CraftedWeaponInternallyCreated : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public bool IsFreeMode;
    public Hero CraftingHero;
    public ItemObject CraftedItemObject;
    public WeaponDesign WeaponDesign;
    public ItemModifier WeaponModifier;
    public string NextCraftedItemId;
    public Hero PlayerHero;
    public Crafting CraftingLogic;

    public CraftedWeaponInternallyCreated(
        CraftingCampaignBehavior craftingCampaignBehavior,
        bool isFreeMode,
        Hero craftingHero,
        ItemObject craftedItemObject,
        WeaponDesign weaponDesign,
        ItemModifier weaponModifier,
        string nextCraftedItemId,
        Hero playerHero,
        Crafting craftingLogic)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        IsFreeMode = isFreeMode;
        CraftingHero = craftingHero;
        CraftedItemObject = craftedItemObject;
        WeaponDesign = weaponDesign;
        WeaponModifier = weaponModifier;
        NextCraftedItemId = nextCraftedItemId;
        PlayerHero = playerHero;
        CraftingLogic = craftingLogic;
    }
}