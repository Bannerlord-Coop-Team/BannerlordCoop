using Autofac;
using Common.Extensions;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.BesiegerCamps.Patches;
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
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Villages.Commands;

public class BesiegerCampDebugCommand
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

    // coop.debug.besiegercamp.set_number_of_troops_killed_on_side
    /// <summary>
    /// Set the number of tropps killed on side 
    /// </summary>
    /// <param name="args">first arg : besiegerCampId ; second arg : value</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("set_number_of_troops_killed_on_side", "coop.debug.BesiegerCamp")]
    public static string SetBesiegerNumberOfTroopsKilledOnSide(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.besiegerCamp.set_foodStocks <besiegerCampId> <foodStocks> ";
        }

        string besiegerCampId = args[0];
        string troopsValueString = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(besiegerCampId, out BesiegerCamp besiegerCamp) == false)
        {
            return $"BesiegerCamp with ID: '{besiegerCampId}' not found";
        }

        if (int.TryParse(troopsValueString, out int troopsValue) == false)
        {
            return $"Argument2: {troopsValueString} is not a int.";
        }

        besiegerCamp.NumberOfTroopsKilledOnSide = troopsValue;

        return $"BesiegerCamp NumberOfTroopsKilledOnSide has changed to: {besiegerCamp.NumberOfTroopsKilledOnSide}";
    }
}
