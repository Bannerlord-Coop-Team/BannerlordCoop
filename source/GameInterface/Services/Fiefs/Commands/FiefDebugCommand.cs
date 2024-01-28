using Autofac;
using Common.Extensions;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Services.Towns.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Fiefs.Commands;

public class FiefDebugCommand
{

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    // coop.debug.fief.set_foodStocks
    /// <summary>
    /// Set the food stocks for a Fief
    /// </summary>
    /// <param name="args">first arg : fiefiId ; second arg : stock value</param>
    /// <returns>strings of all the towns</returns>
    [CommandLineArgumentFunction("set_foodStocks", "coop.debug.fief")]
    public static string SetFoodStocks(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.fief.set_foodStocks <FiefId> <foodStocks> ";
        }

        string fiefId = args[0];
        string foodStocksString = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(fiefId, out Fief fief) == false)
        {
            return $"Fief with ID: '{fiefId}' not found";
        }

        if (float.TryParse(foodStocksString, out float foodStocks) == false)
        {
            return $"Argument2: {foodStocksString} is not a float.";
        }

        fief.FoodStocks = foodStocks;

        return $"Fief food stocks has changed to: {fief.FoodStocks}";
    }
}
