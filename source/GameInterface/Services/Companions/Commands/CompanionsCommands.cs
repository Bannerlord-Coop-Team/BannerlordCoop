using Autofac;
using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Companions.Commands;

internal class CompanionsCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<CompanionsCommands>();

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
    /// View a list of all wanderers in the game
    /// </summary>
    [CommandLineArgumentFunction("listwanderers", "coop.debug.companions")]
    public static string ListWanderersCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.IsWanderer)
            {
                stringBuilder.AppendLine(hero.CurrentSettlement + " (" + hero.Name.ToString() + ")");
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
