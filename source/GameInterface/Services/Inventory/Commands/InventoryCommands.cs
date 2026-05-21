using Autofac;
using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Smithing.Commands;

internal class InventoryCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<SmithingCommands>();

    /// <summary>
    /// View item ids in player inventories
    /// </summary>
    [CommandLineArgumentFunction("itemids", "coop.debug.inventory")]
    public static string ViewItemIdsCommand(List<string> strings)
    {
        if (strings.Count == 0)
        {
            return "Hero name argument required.";
        }

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                stringBuilder.AppendLine(hero.Name.ToString());
                foreach (var rosterElement in hero.PartyBelongedTo.ItemRoster)
                {
                    stringBuilder.AppendLine(rosterElement.EquipmentElement.Item.StringId + ": " + rosterElement._amount);
                }
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
    /// View item values in player inventories
    /// </summary>
    [CommandLineArgumentFunction("itemvalues", "coop.debug.inventory")]
    public static string ViewItemValuesCommand(List<string> strings)
    {
        if (strings.Count == 0)
        {
            return "Hero name argument required.";
        }

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                stringBuilder.AppendLine(hero.Name.ToString());
                foreach (var rosterElement in hero.PartyBelongedTo.ItemRoster)
                {
                    stringBuilder.AppendLine(rosterElement.EquipmentElement.Item.StringId + ": " + rosterElement.EquipmentElement.Item.Value + "(" + rosterElement.EquipmentElement.ItemValue + ")");
                }
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
