using Common.Logging;
using GameInterface.Services.Caravans.Commands;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Buildings.Commands;

internal class BuildingDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravansCommands>();

    /// <summary>
    /// View all buildings in a town with their levels and progress
    /// </summary>
    [CommandLineArgumentFunction("town_buildings", "coop.debug.buildings")]
    public static string ViewTownBuildingsCommand(List<string> strings)
    {
        if (strings.Count == 0) return "Usage: coop.debug.buildings.town_buildings <settlementId>";

        StringBuilder stringBuilder = new StringBuilder();
        Settlement settlement = Settlement.Find(strings[0]);
        if (settlement == null)
        {
            return $"Settlement with id: '{strings[0]}' not found";
        }

        stringBuilder.AppendLine($"{settlement.Name}");
        foreach (var building in settlement.Town.Buildings)
        {
            stringBuilder.AppendLine($"Name: {building.Name}");
            stringBuilder.AppendLine($"Level: {building.CurrentLevel}");
            stringBuilder.AppendLine($"Progress: {building.BuildingProgress}");
        }

        return stringBuilder.ToString();
    }
}
