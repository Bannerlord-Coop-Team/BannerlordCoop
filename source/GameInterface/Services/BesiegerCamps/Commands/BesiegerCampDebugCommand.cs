using Autofac;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.Library.CommandLineFunctionality;

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
    /// Set the number of tropps killed
    /// </summary>
    /// <param name="args">first arg : besiegerCampId ; second arg : value</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("set_number_of_troops_killed_on_side", "coop.debug.BesiegerCamp")]
    public static string SetBesiegerCampNumberOfTroopsKilledOnSide(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.besiegerCamp.set_number_of_troops_killed_on_side <besiegerCampId> <value> ";
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

    // coop.debug.besiegercamp.set_progress
    /// <summary>
    /// Set siege preparations progress
    /// </summary>
    /// <param name="args">first arg : besiegerCampId ; second arg : value</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("coop.debug.besiegercamp.set_progress", "coop.debug.BesiegerCamp")]
    public static string SetBesiegerCampPreparationsProgress(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.besiegerCamp.set_progress <besiegerCampId> <progress> ";
        }

        string besiegerCampId = args[0];
        string percentageValueString = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(besiegerCampId, out BesiegerCamp besiegerCamp) == false)
        {
            return $"BesiegerCamp with ID: '{besiegerCampId}' not found";
        }

        if (float.TryParse(percentageValueString, out float progressPercentage) == false)
        {
            return $"Argument2: {percentageValueString} is not a int.";
        }

        besiegerCamp.SiegeEngines.SiegePreparations.SetProgress(progressPercentage);

        return $"BesiegerCamp preparations progress has changed to: {besiegerCamp.SiegeEngines.SiegePreparations.Progress}";
    }

    // coop.debug.besiegercamp.set_siege_strategy
    /// <summary>
    /// Set the siege strategy for a besieger camp
    /// </summary>
    /// <param name="args">first arg: besiegerCampId; second arg: strategyId</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("set_siege_strategy", "coop.debug.BesiegerCamp")]
    public static string SetBesiegerCampSiegeStrategy(List<string> args)
    {
        string getPossibleStragegyIds() => string.Join(Environment.NewLine, SiegeStrategy.All.Select(x => x.StringId));
        string idTipMsg = $"{Environment.NewLine}SiegeStrategy Id must be one of the following:{getPossibleStragegyIds()}";

        if (args.Count != 2)
        {
            return "Usage: coop.debug.besiegerCamp.set_siege_strategy <besiegerCampId> <strategyId>" + idTipMsg;
        }

        string besiegerCampId = args[0];
        string strategyId = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var besiegerCamp) == false)
        {
            return $"BesiegerCamp with ID: '{besiegerCampId}' not found";
        }

        // Attempt to create or retrieve the SiegeStrategy based on the strategyId
        SiegeStrategy siegeStrategy = SiegeStrategy.All.FirstOrDefault(x => string.Equals(x.StringId, strategyId, StringComparison.OrdinalIgnoreCase));
        if (siegeStrategy == null)
        {
            return $"Invalid SiegeStrategy ID :'{strategyId}'{idTipMsg}";
        }

        // Assign the strategy to the besieger camp
        besiegerCamp.SiegeStrategy = siegeStrategy;

        return $"SiegeStrategy for BesiegerCamp {besiegerCampId} has been set to: {siegeStrategy.StringId}";
    }
}