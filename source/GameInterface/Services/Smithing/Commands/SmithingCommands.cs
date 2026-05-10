using Autofac;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Smithing.Commands;

internal class SmithingCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<SmithingCommands>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// Give debug crafting materials to heroes with a given name
    /// </summary>
    [CommandLineArgumentFunction("givesupplies", "coop.debug.crafting")]
    public static string SmithingSuppliesCommand(List<string> strings)
    {
        if (strings.Count == 0)
        {
            return "Hero name argument required.";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager.";
        }

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                var itemsToAdd = new Dictionary<string, int>()
                {
                    { "hardwood", 100 },
                    { "iron", 50 },
                    { "charcoal", 100 },
                    { "ironIngot1", 50 },
                    { "ironIngot2", 50 },
                    { "ironIngot3", 50 },
                    { "ironIngot4", 50 },
                    { "ironIngot5", 50 },
                    { "ironIngot6", 50 },
                    { "empire_sword_4_t4", 3 }
                };

                foreach (var itemId in itemsToAdd.Keys)
                {
                    if (!objectManager.TryGetObject(itemId, out ItemObject itemObject)) {
                        stringBuilder.AppendLine("Failed to retrieve object for ItemObject id: " + itemId);
                    }
                    else
                    {
                        hero.PartyBelongedTo.ItemRoster.AddToCounts(itemObject, itemsToAdd[itemId]);
                    }
                }

                stringBuilder.AppendLine(strings[0] + " was given smithing supplies.");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Hero not found.";
    }

    [CommandLineArgumentFunction("townorders", "coop.debug.crafting")]
    public static string ViewTownOrdersCommand(List<string> strings)
    {
        if (strings.Count == 0)
        {
            return "Town name argument required.";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager.";
        }

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var town in Town.AllTowns)
        {
            if (town.Name.ToString() == strings[0])
            {
                stringBuilder.AppendLine("Target town " + town.Name.ToString() + " has orders:");
                CraftingOrder[] slots = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftingOrders[town].Slots;
                foreach (CraftingOrder order in slots)
                {
                    stringBuilder.AppendLine("Order slot: " + order?.OrderDifficulty + " for hero: " + order?.OrderOwner);
                }
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Town not found.";
    }

    [CommandLineArgumentFunction("addtownorder", "coop.debug.crafting")]
    public static string AddTestingTownOrderCommand(List<string> strings)
    {
        var testingHeroName = "Vaminesa the Minter"; // Random hero in Danustica, primary testing town

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == testingHeroName)
            {
                for (int i = 0; i < 6; i++)
                {
                    Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>().CreateTownOrder(hero, i);
                }

                stringBuilder.AppendLine("Orders have been added for " + testingHeroName + " in " + hero.CurrentSettlement.Town.Name.ToString());
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Town not found.";
    }

    [CommandLineArgumentFunction("addcrafteditems", "coop.debug.crafting")]
    public static string AddCraftedItemCommand(List<string> strings)
    {
        if (strings.Count == 0)
        {
            return "Hero name argument required.";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager.";
        }

        var craftedItemPrefix = "crafted_item_";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                int craftedItemCount = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftedItemCount;
                for (int i = 0; i < craftedItemCount; i++)
                {
                    string craftedItemId = craftedItemPrefix + i.ToString();
                    if (!objectManager.TryGetObject(craftedItemId, out ItemObject itemObject))
                    {
                        stringBuilder.AppendLine("Failed to retrieve object for ItemObject id: " + craftedItemId);
                    }
                    else
                    {
                        hero.PartyBelongedTo.ItemRoster.AddToCounts(itemObject, 1);
                    }
                }

                stringBuilder.AppendLine(strings[0] + " was given all crafted items.");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Town not found.";
    }
}
