using Autofac;
using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Serilog;
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
        if (ModInformation.IsClient) return "Command can only be run on the server.";

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
                    if (!objectManager.TryGetObject(itemId, out ItemObject itemObject)) 
                    {
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

    /// <summary>
    /// View town orders for a specified town
    /// </summary>
    [CommandLineArgumentFunction("townorders", "coop.debug.crafting")]
    public static string ViewTownOrdersCommand(List<string> strings)
    {
        if (strings.Count == 0) return "Town name argument required.";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

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

    /// <summary>
    /// Add orders to a town by a hero in that town
    /// </summary>
    [CommandLineArgumentFunction("addtownorder", "coop.debug.crafting")]
    public static string AddTestingTownOrderCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        //if (strings.Count == 0) return "Hero name argument required.";

        var heroName = "Zoros the Brewer"; // Example hero name: "Vaminesa the Minter"

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == heroName)
            {
                for (int i = 0; i < 6; i++)
                {
                    Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>().CreateTownOrder(hero, i);
                }

                stringBuilder.AppendLine("Orders have been added for " + heroName + " in " + hero.CurrentSettlement.Town.Name.ToString());
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Town not found.";
    }

    /// <summary>
    /// Add all existing crafted items to a given hero
    /// </summary>
    [CommandLineArgumentFunction("addcrafteditems", "coop.debug.crafting")]
    public static string AddCraftedItemCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name argument required.";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

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

    /// <summary>
    /// View crafted item history, showing all players on server and current player on client
    /// </summary>
    [CommandLineArgumentFunction("crafteditemhistory", "coop.debug.crafting")]
    public static string ViewCraftedItemHistoryCommand(List<string> strings)
    {
        if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            foreach (KeyValuePair<string, List<string>> craftedItemHistory in coopSessionProvider.CoopSession.CraftingPlayerData.PlayerCraftedItemsHistory)
            {
                stringBuilder.AppendLine(craftedItemHistory.Key);
                foreach (string craftedItemId in craftedItemHistory.Value)
                {
                    stringBuilder.AppendLine(craftedItemId);
                }
            }
        }
        else
        {
            CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            foreach (ItemObject item in craftingCampaignBehavior._cratingItemsHistory)
            {
                stringBuilder.AppendLine(item.StringId);
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Error finding crafting player data or no crafted item history";
    }

    /// <summary>
    /// View crafted pieces xp, showing all players on server and current player on client
    /// </summary>
    [CommandLineArgumentFunction("craftingpiecesxp", "coop.debug.crafting")]
    public static string ViewPartsXpCommand(List<string> strings)
    {
        if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            foreach (KeyValuePair<string, Dictionary<string, float>> playerPartXp in coopSessionProvider.CoopSession.CraftingPlayerData.PlayerOpenNewPartXpDictionary)
            {
                stringBuilder.AppendLine(playerPartXp.Key);
                foreach (KeyValuePair<string, float> partXp in playerPartXp.Value)
                {
                    stringBuilder.AppendLine(partXp.Key + ": " + partXp.Value);
                }
            }
        }
        else
        {
            CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            foreach (KeyValuePair<CraftingTemplate, float> partXp in craftingCampaignBehavior._openNewPartXpDictionary)
            {
                stringBuilder.AppendLine(partXp.Key + ": " + partXp.Value);
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Error finding crafting player data or no parts xp data";
    }

    /// <summary>
    /// View unlocked crafted pieces, showing all players on server and current player on client
    /// </summary>
    [CommandLineArgumentFunction("unlockedcraftingpieces", "coop.debug.crafting")]
    public static string ViewUnlockedCraftingPieces(List<string> strings)
    {
        if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            foreach (KeyValuePair<string, Dictionary<string, List<string>>> playerUnlockedPieces in coopSessionProvider.CoopSession.CraftingPlayerData.PlayerOpenedPartsDictionary)
            {
                stringBuilder.AppendLine(playerUnlockedPieces.Key);
                foreach (KeyValuePair<string, List<string>> templateUnlockedPieces in playerUnlockedPieces.Value)
                {
                    stringBuilder.AppendLine(templateUnlockedPieces.Key + ": " + templateUnlockedPieces.Value.Count);
                }
            }
        }
        else
        {
            CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            foreach (KeyValuePair<CraftingTemplate, List<CraftingPiece>> templateUnlockedPieces in craftingCampaignBehavior._openedPartsDictionary)
            {
                stringBuilder.AppendLine(templateUnlockedPieces.Key + ": " + templateUnlockedPieces.Value.Count);
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Error finding crafting player data or no unlocked parts";
    }
}
