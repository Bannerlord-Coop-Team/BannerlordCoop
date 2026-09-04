using Common.Logging;
using Common.Util;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Interfaces;

public interface ICraftingCampaignBehaviorInterface : IGameAbstraction
{
    int DoSmelting(CraftingCampaignBehavior craftingBehavior, Hero craftingHero, EquipmentElement equipmentElement);
    int DoRefinement(CraftingCampaignBehavior craftingBehavior, Hero craftingHero, Crafting.RefiningFormula formula);
    bool CreateCraftedWeaponInternal(CraftingCampaignBehavior craftingBehavior, Hero craftingHero, CraftingTemplate craftingTemplate, ItemModifierGroup itemModifierGroup, WeaponDesignElement[] usedPieces, ItemModifier weaponModifier, CultureObject culture, bool isFreeMode, TextObject name, string weaponName, string nextCraftedItemI, out int newHeroCraftingStamina);
    ItemObject CreateAndRegisterCraftedItem(WeaponDesign weaponDesign, TextObject name, CultureObject culture, ItemModifierGroup itemModifierGroup, string craftedItemId);
    void AddCraftedItemToRoster(ItemRoster itemRoster, ItemModifier weaponModifier, ItemObject craftedItemObject);
    void DailyTickSettlement(CraftingCampaignBehavior craftingBehavior, Settlement settlement);
    bool TryGetCraftingBehavior(out CraftingCampaignBehavior craftingBehavior);
}

public class CraftingCampaignBehaviorInterface : ICraftingCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorInterface>();
    
    private readonly IObjectManager objectManager;

    public CraftingCampaignBehaviorInterface(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public int DoSmelting(CraftingCampaignBehavior craftingBehavior, Hero craftingHero, EquipmentElement equipmentElement)
    {
        int heroCraftingStamina = craftingBehavior.GetHeroCraftingStamina(craftingHero);

        ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
        int[] smeltingOutputForItem = Campaign.Current.Models.SmithingModel.GetSmeltingOutputForItem(equipmentElement.Item);

        // Needed to prevent spam clicking to smelt more than actually available
        if (itemRoster.FindIndexOfElement(equipmentElement) < 0) return heroCraftingStamina;
        itemRoster.AddToCounts(equipmentElement, -1);

        for (int i = 8; i >= 0; i--)
        {
            if (smeltingOutputForItem[i] != 0)
            {
                itemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i), smeltingOutputForItem[i]);
            }
        }

        craftingHero.AddSkillXp(DefaultSkills.Crafting, (float)Campaign.Current.Models.SmithingModel.GetSkillXpForSmelting(equipmentElement.Item));

        int energyCostForSmelting = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmelting(equipmentElement.Item, craftingHero);
        heroCraftingStamina -= energyCostForSmelting;
        craftingBehavior.SetHeroCraftingStamina(craftingHero, heroCraftingStamina);

        CampaignEventDispatcher.Instance.OnEquipmentSmeltedByHero(craftingHero, equipmentElement);

        return heroCraftingStamina;
    }

    public int DoRefinement(CraftingCampaignBehavior craftingBehavior, Hero craftingHero, Crafting.RefiningFormula formula)
    {
        int heroCraftingStamina = craftingBehavior.GetHeroCraftingStamina(craftingHero);

        ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
        if (formula.Input1Count > 0)
        {
            ItemObject craftingMaterialItem = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Input1);

            // Needed to prevent spam clicking to refine more than actually available
            if (itemRoster.FindIndexOfElement(new EquipmentElement(craftingMaterialItem, null, null, false)) < 0) return heroCraftingStamina;
            itemRoster.AddToCounts(craftingMaterialItem, -formula.Input1Count);
        }
        if (formula.Input2Count > 0)
        {
            ItemObject craftingMaterialItem2 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Input2);

            // Needed to prevent spam clicking to refine more than actually available
            if (itemRoster.FindIndexOfElement(new EquipmentElement(craftingMaterialItem2, null, null, false)) < 0) return heroCraftingStamina;
            itemRoster.AddToCounts(craftingMaterialItem2, -formula.Input2Count);
        }
        if (formula.OutputCount > 0)
        {
            ItemObject craftingMaterialItem3 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Output);
            itemRoster.AddToCounts(craftingMaterialItem3, formula.OutputCount);
        }
        if (formula.Output2Count > 0)
        {
            ItemObject craftingMaterialItem4 = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(formula.Output2);
            itemRoster.AddToCounts(craftingMaterialItem4, formula.Output2Count);
        }

        craftingHero.AddSkillXp(DefaultSkills.Crafting, (float)Campaign.Current.Models.SmithingModel.GetSkillXpForRefining(ref formula));

        int energyCostForRefining = Campaign.Current.Models.SmithingModel.GetEnergyCostForRefining(ref formula, craftingHero);
        heroCraftingStamina = craftingBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForRefining;
        craftingBehavior.SetHeroCraftingStamina(craftingHero, heroCraftingStamina);

        CampaignEventDispatcher.Instance.OnItemsRefined(craftingHero, formula);

        return heroCraftingStamina;
    }

    public bool CreateCraftedWeaponInternal(
        CraftingCampaignBehavior craftingBehavior,
        Hero craftingHero,
        CraftingTemplate craftingTemplate,
        ItemModifierGroup itemModifierGroup,
        WeaponDesignElement[] usedPieces,
        ItemModifier weaponModifier,
        CultureObject culture,
        bool isFreeMode,
        TextObject name,
        string weaponName,
        string nextCraftedItemId,
        out int newHeroCraftingStamina)
    {
        WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(weaponName), usedPieces);
        if (isFreeMode)
        {
            weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
        }

        ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
        int[] smithingCostsForWeaponDesign = Campaign.Current.Models.SmithingModel.GetSmithingCostsForWeaponDesign(weaponDesign);

        newHeroCraftingStamina = craftingBehavior.GetHeroCraftingStamina(craftingHero);

        // Reject crafted weapons with materials that no longer exist
        for (int i = 8; i >= 0; i--)
        {
            if (smithingCostsForWeaponDesign[i] >= 0) continue;
            var material = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i);
            if (itemRoster.GetItemNumber(material) + smithingCostsForWeaponDesign[i] < 0)
            {
                Logger.Warning($"Rejecting crafted weapon of template {weaponDesign.Template.StringId} due to a lack of {material.StringId}.");
                return false;
            }
        }

        // Implement CraftingCampaignBehavior.SpendMaterials(weaponDesign) here as it needs the party roster, MainParty on server won't be correct
        for (int i = 8; i >= 0; i--)
        {
            if (smithingCostsForWeaponDesign[i] != 0)
            {
                itemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i), smithingCostsForWeaponDesign[i]);
            }
        }

        var craftedItemObject = CreateAndRegisterCraftedItem(weaponDesign, name, culture, itemModifierGroup, nextCraftedItemId);

        int energyCostForSmithing = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(craftedItemObject, craftingHero);
        newHeroCraftingStamina = craftingBehavior.GetHeroCraftingStamina(craftingHero) - energyCostForSmithing;
        craftingBehavior.SetHeroCraftingStamina(craftingHero, newHeroCraftingStamina);

        CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !isFreeMode);

        return true;
    }

    public ItemObject CreateAndRegisterCraftedItem(WeaponDesign weaponDesign, TextObject name, CultureObject culture, ItemModifierGroup itemModifierGroup, string craftedItemId)
    {
        ItemObject craftedItemObject = null;
        using (new AllowedThread())
        {
            craftedItemObject = new();
            ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);
            Crafting.GenerateItem(
                weaponDesign,
                name,
                culture,
                itemModifierGroup,
                ref craftedItemObject,
                craftedItemId);
        }

        objectManager.AddExisting(craftedItemId, craftedItemObject);
        MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);

        return craftedItemObject;
    }

    public void AddCraftedItemToRoster(ItemRoster itemRoster, ItemModifier weaponModifier, ItemObject craftedItemObject)
    {
        if (weaponModifier == null)
        {
            itemRoster.AddToCounts(craftedItemObject, 1);
        }
        else
        {
            EquipmentElement rosterElement = new EquipmentElement(craftedItemObject, weaponModifier, null, false);
            itemRoster.AddToCounts(rosterElement, 1);
        }
    }

    public void DailyTickSettlement(CraftingCampaignBehavior craftingBehavior, Settlement settlement)
    {
        if (settlement.IsTown && craftingBehavior.CraftingOrders[settlement.Town].IsThereAvailableSlot())
        {
            List<Hero> list = new List<Hero>(settlement.HeroesWithoutParty);
            foreach (MobileParty mobileParty in settlement.Parties)
            {
                // Prevents adding town orders with player hero order owners
                if (mobileParty.LeaderHero != null && !mobileParty.IsMainParty && !mobileParty.IsPlayerParty())
                {
                    list.Add(mobileParty.LeaderHero);
                }
            }
            foreach (Hero hero in list)
            {
                // Prevents adding town orders with player hero order owners
                if (!hero.IsPlayerHero() && MBRandom.RandomFloat <= 0.05f)
                {
                    int availableSlot = craftingBehavior.CraftingOrders[settlement.Town].GetAvailableSlot();
                    if (availableSlot <= -1)
                    {
                        break;
                    }
                    craftingBehavior.CreateTownOrder(hero, availableSlot);
                }
            }
        }
    }

    public bool TryGetCraftingBehavior(out CraftingCampaignBehavior craftingBehavior)
    {
        craftingBehavior = Campaign.Current?.GetCampaignBehavior<CraftingCampaignBehavior>();
        if (craftingBehavior != null) return true;

        Logger.Debug("Skipping crafting update because the campaign behavior is unavailable");
        return false;
    }
}