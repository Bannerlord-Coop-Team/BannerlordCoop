using Autofac;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Template.Commands;

internal class SettlementCommands
{
    [CommandLineArgumentFunction("enter_random_castle", "coop.debug.settlements")]
    public static string EnterRandomCastle(List<string> strings)
    {
        var castles = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsCastle).ToArray();

        Random random = new Random();

        var randomCastle = castles[random.Next(castles.Length)];

        EncounterManager.StartSettlementEncounter(MobileParty.MainParty, randomCastle);

        return $"Entering {randomCastle.Name} castle";
    }

    [CommandLineArgumentFunction("get_town_name", "coop.debug.settlements")]
    public static string GetTownName(List<string> strings)
    {
        if (strings.Count != 1) return "Invalid usage, expected \"get_town_name <settlment id>\"";

        string settlementId = strings.Single();

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get town name";

        var objectManager = container.Resolve<IObjectManager>();

        if (objectManager.Contains(settlementId) == false) return $"{settlementId} does not exist";

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false) 
            throw new Exception($"{settlementId} was in object manager but was unable to be resolved");

        return $"Settlement Name: {settlement.Name}";
    }

    // coop.debug.settlements.set_enemies_spotted town_ES3 45.4
    /// <summary>
    /// Changes the NumberOfEnemiesSpottedAround
    /// </summary>
    /// <param name="args">the settlement and float value</param>
    /// <returns>info that is was succesfull</returns>
    [CommandLineArgumentFunction("set_enemies_spotted", "coop.debug.settlements")]
    public static string SetEnemiesSpotted(List<string> args)
    {
        if (ModInformation.IsClient) return "This function can only be used by the server";

        if (args.Count != 2) return "Invalid usage, expected \"set_enemies_spotted <settlment id> <float_value>\"";

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get Settlement";

        var objectManager = container.Resolve<IObjectManager>();

        string settlementId = args[0];

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            return $"Settlement: {settlementId} was not found.";

        try
        {
            settlement.NumberOfEnemiesSpottedAround = float.Parse(args[1]);
        } catch (Exception ex)
        {
            return $"Error setting the value: {args[1]} to a float.";
        }

        return $"Successfully set the Settlement ({settlementId}) NumberOfEnemiesSpottedAround to '{args[1]}'";
    }


    // coop.debug.settlements.set_allies_spotted town_ES3 45.4
    /// <summary>
    /// Changes the NumberOfAlliesSpottedAround
    /// </summary>
    /// <param name="args">the settlement and float value</param>
    /// <returns>info that is was succesful</returns>
    [CommandLineArgumentFunction("set_allies_spotted", "coop.debug.settlements")]
    public static string SetAlliesSpotted(List<string> args)
    {
        if (ModInformation.IsClient) return "This function can only be used by the server";

        if (args.Count != 2) return "Invalid usage, expected \"set_enemies_spotted <settlment id> <float_value>\"";

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get Settlement";

        var objectManager = container.Resolve<IObjectManager>();

        string settlementId = args[0];

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            return $"Settlement: {settlementId} was not found.";

        try
        {
            settlement.NumberOfAlliesSpottedAround = float.Parse(args[1]);
        }
        catch (Exception ex)
        {
            return $"Error setting the value: {args[1]} to a float.";
        }

        return $"Successfully set the Settlement ({settlementId}) NumberOfAlliesSpottedAround to '{args[1]}'";
    }


    // coop.debug.settlements.set_bribe_paid town_ES3 50.0
    /// <summary>
    /// Changes the BribePaid
    /// </summary>
    /// <param name="args">the settlement and int value</param>
    /// <returns>info that is was succesful</returns>
    [CommandLineArgumentFunction("set_bribe_paid", "coop.debug.settlements")]
    public static string SetBribePaid(List<string> args)
    {
        if (ModInformation.IsClient) return "This function can only be used by the server";

        if (args.Count != 2) return "Invalid usage, expected \"set_bribe_paid <settlment id> <int_value>\"";

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get Settlement";

        var objectManager = container.Resolve<IObjectManager>();

        string settlementId = args[0];

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            return $"Settlement: {settlementId} was not found.";

        try
        {
            settlement.BribePaid = int.Parse(args[1]);
        }
        catch (Exception ex)
        {
            return $"Error setting the value: {args[1]} to a int.";
        }

        return $"Successfully set the Settlement ({settlementId}) BribePaid to '{args[1]}'";
    }



    private static readonly PropertyInfo SettlementHitPoints = typeof(Settlement).GetProperty(nameof(Settlement.SettlementHitPoints));

    // coop.debug.settlements.set_hit_points town_ES3 50.4
    /// <summary>
    /// Changes the SettlementHitPoints
    /// </summary>
    /// <param name="args">the settlement and float value</param>
    /// <returns>info that is was succesful</returns>
    [CommandLineArgumentFunction("set_hit_points", "coop.debug.settlements")]
    public static string SetHitPoints(List<string> args)
    {
        if (ModInformation.IsClient) return "This function can only be used by the server";

        if (args.Count != 2) return "Invalid usage, expected \"set_bribe_paid <settlment id> <int_value>\"";

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get Settlement";

        var objectManager = container.Resolve<IObjectManager>();

        string settlementId = args[0];

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            return $"Settlement: {settlementId} was not found.";

        try
        {
            SettlementHitPoints.SetValue(settlement, float.Parse(args[1]));
        }
        catch (Exception ex)
        {
            return $"Error setting the value: {args[1]} to a float.";
        }

        return $"Successfully set the Settlement ({settlementId}) BribePaid to '{args[1]}'";
    }

    // Located in Modules\SandBox\ModuleData\settlements.xml
    // POROS EXAMPLE
    // coop.debug.settlements.info town_ES3 
    /// <summary>
    /// Gives a bunch of information on a settlement.
    /// </summary>
    /// <param name="args">settlement name</param>
    /// <returns>info about the settlement</returns>
    [CommandLineArgumentFunction("info", "coop.debug.settlements")]
    public static string Info(List<string> args)
    {

        if (args.Count != 1) return "Invalid usage, expected \"info <settlment id>\"";

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get Settlement";

        var objectManager = container.Resolve<IObjectManager>();

        string settlementId = args.Single();

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            return $"Settlement: {settlementId} was not found.";

        StringBuilder sb = new();

        string lastAttackerParty = settlement.LastAttackerParty?.ArmyName.ToString() ?? "None";

        sb.AppendLine($"------------------- SETTLEMENT: {settlement.Name} -------------------");
        sb.AppendLine($"NumberOfEnemiesSpottedAround: '{settlement.NumberOfEnemiesSpottedAround}'");
        sb.AppendLine($"NumberOfAlliesSpottedAround: '{settlement.NumberOfAlliesSpottedAround}'");
        sb.AppendLine($"BribePaid: {settlement.BribePaid}");
        sb.AppendLine($"SettlementHitPoints: '{settlement.SettlementHitPoints}'");
        sb.AppendLine($"GarrisonWagePaymentLimit: '{settlement.GarrisonWagePaymentLimit}'");
        sb.AppendLine($"LastAttackerParty: '{lastAttackerParty}'");
        sb.AppendLine($"LastThreatTime:  '{settlement.LastThreatTime}'");
        sb.AppendLine($"CurrentSiegeState:   '{settlement.CurrentSiegeState}'");
        sb.AppendLine($"Militia :   '{settlement.Militia}'");
        sb.AppendLine($"LastVisitTimeOfOwner  :   '{settlement.LastVisitTimeOfOwner}'");
        sb.AppendLine($"ClaimedBy   :   '{settlement.ClaimedBy}'");
        sb.AppendLine($"ClaimValue    :   '{settlement.ClaimValue}'");
        sb.AppendLine($"CanBeClaimed     :   '{Convert.ToBoolean(settlement.CanBeClaimed)}'");
        sb.AppendLine($"------------------- SETTLEMENT: {settlement.Name} -------------------");

        return sb.ToString();
    }
}
