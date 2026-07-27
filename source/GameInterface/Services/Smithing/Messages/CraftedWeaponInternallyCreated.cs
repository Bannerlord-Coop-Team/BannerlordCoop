using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

public record CraftedWeaponInternallyCreated : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public bool IsFreeMode;
    public Hero CraftingHero;
    public TextObject Name;
    public BasicCultureObject CultureObject;
    public WeaponDesign WeaponDesign;
    public ItemModifier WeaponModifier;
    public Hero PlayerHero;
    public Crafting CraftingLogic;

    public CraftedWeaponInternallyCreated(
        CraftingCampaignBehavior craftingCampaignBehavior,
        bool isFreeMode,
        Hero craftingHero,
        TextObject name,
        BasicCultureObject cultureObject,
        WeaponDesign weaponDesign,
        ItemModifier weaponModifier,
        Hero playerHero,
        Crafting craftingLogic)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        IsFreeMode = isFreeMode;
        CraftingHero = craftingHero;
        Name = name;
        CultureObject = cultureObject;
        WeaponDesign = weaponDesign;
        WeaponModifier = weaponModifier;
        PlayerHero = playerHero;
        CraftingLogic = craftingLogic;
    }
}