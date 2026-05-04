using Autofac;
using Common.Logging;
using GameInterface.Services.CharacterDevelopers.Handlers;
using GameInterface.Services.ObjectManager;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
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
}
