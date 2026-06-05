using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static HarmonyLib.Code;
using static TaleWorlds.CampaignSystem.Army;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Armies.Commands;

/// <summary>
/// Commands for <see cref="Army"/>
/// </summary>
public class ArmyDebugCommand
{
    // coop.debug.army.list
    /// <summary>
    /// Lists all the current Army
    /// </summary>
    [CommandLineArgumentFunction("list", "coop.debug.army")]
    public static string ListArmy(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();



        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to resolve {nameof(ArmyRegistry)}";
        }

        foreach (var army in Kingdom.All.SelectMany(kingdom => kingdom.Armies))
        {
            if (!objectManager.TryGetId(army, out var armyId))
            {
                stringBuilder.AppendLine($"Unable to get id for Army Name: '{army.Name}'");
                continue;
            }

            stringBuilder.AppendLine($"Name: '{army.Name}'");
            stringBuilder.AppendLine($"StringId: '{armyId}'");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.army.create empire town_EN2 lord_1_1 Raider
    // coop.debug.army.mobile_party_add Army_Created_1 lord_1_3_party_1
    // coop.debug.army.destroy Army_Created_1 NotEnoughParty
    /// <summary>
    /// Creates a new army on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("create", "coop.debug.army")]
    public static string CreateArmy(List<string> args)
    {
        var sb = new StringBuilder();
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 4)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Usage: coop.debug.kingdom.create <kingdomId> <targetSettlmentId> <heroLeaderId> <armyType>");
            stringBuilder.Append(GetArmyTypesUsage(stringBuilder));

            return stringBuilder.ToString();
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return "Unable to get ObjectManager";
        }

        var kingdomId = args[0];
        if (objectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom) == false)
        {
            return $"Unable to get Kingdom with {kingdomId}";
        }

        var targetSettlmentId = args[1];
        if (objectManager.TryGetObject<Settlement>(targetSettlmentId, out var targetSettlment) == false)
        {
            return $"Unable to get Settlement with {targetSettlmentId}";
        }

        var heroLeaderId = args[2];
        if (objectManager.TryGetObject<Hero>(heroLeaderId, out var armyLeader) == false)
        {
            return $"Unable to get Hero with {heroLeaderId}";
        }

        var armyTypeInt = args[3];
        if (Enum.TryParse(armyTypeInt, true, out ArmyTypes armyType) == false)
        {
            return $"Unable to cast {armyTypeInt} to {nameof(ArmyTypes)}\n" +
                GetArmyTypesUsage();
        }

        kingdom.CreateArmy(armyLeader, targetSettlment, armyType);
        var army = armyLeader.PartyBelongedTo?.Army;
        sb.AppendLine($"Created army {army.Name.ToString()}");
        return sb.ToString();
    }

    private static string GetArmyTypesUsage(StringBuilder stringBuilder = null)
    {
        stringBuilder = stringBuilder ?? new StringBuilder();

        stringBuilder.Append($"\tArmy.ArmyTypes = [");

        foreach (var armyTypeEnum in Enum.GetNames(typeof(ArmyTypes)).Zip(Enum.GetValues(typeof(ArmyTypes)).Cast<int>()))
        {
            stringBuilder.AppendLine($"\t\t{armyTypeEnum.Item1} = {armyTypeEnum.Item2}");
        }

        stringBuilder.Append("\t]");

        return stringBuilder.ToString();
    }

    // coop.debug.army.destroy Army_Created_1 NotEnoughParty
    /// <summary>
    /// Deletes an army on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("destroy", "coop.debug.army")]
    public static string DestroyArmy(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.kingdom.destroy <armyId> <disbandArmyReason>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        var armyId = args[0];
        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        var disbandArmyReason = args[1];
        if (Enum.TryParse(disbandArmyReason, true, out ArmyDispersionReason reason) == false)
        {
            return $"Unable to cast {disbandArmyReason} to {nameof(ArmyDispersionReason)}\n" +
                GetArmyDispersionReasonUsage();
        }
        var armyName = army.Name.ToString();
        DisbandArmyAction.ApplyInternal(army, reason);

        return $"Destroyed army {armyName} with id {armyId}";
    }

    private static string GetArmyDispersionReasonUsage(StringBuilder stringBuilder = null)
    {
        stringBuilder = stringBuilder ?? new StringBuilder();

        stringBuilder.Append($"\t{nameof(ArmyDispersionReason)} = [");

        foreach (var armyTypeEnum in Enum.GetNames(typeof(ArmyDispersionReason)).Zip(Enum.GetValues(typeof(ArmyDispersionReason)).Cast<int>()))
        {
            stringBuilder.AppendLine($"\t\t{armyTypeEnum.Item1} = {armyTypeEnum.Item2}");
        }

        stringBuilder.Append("\t]");

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_list Army_Created_1
    /// <summary>
    /// Lists all the current Mobile Parties for an Army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("mobile_party_list", "coop.debug.army")]
    public static string GetMobilePartyList(List<string> args)
    {

        var stringBuilder = new StringBuilder();


        if (args.Count != 1)
        {

            return "Usage: coop.debug.army.mobile_party_list <ArmyId>";
        }

        string armyId = args[0];


        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        foreach (var mobileParty in army.Parties)
        {
            stringBuilder.AppendLine($"Name: {mobileParty.Name}\nStringId: {mobileParty.StringId}");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_add Army_Created_1 lord_1_34_party_1
    /// <summary>
    /// Add a Mobile Party to an Army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("mobile_party_add", "coop.debug.army")]
    public static string AddMobileParty(List<string> args)
    {

        var stringBuilder = new StringBuilder();


        if (args.Count != 2)
        {

            return "Usage: coop.debug.army.mobile_party_add <ArmyId> <MobilePartyId>";
        }

        string armyId = args[0];
        string mobilePartyId = args[1];


        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
        {
            return $"Unable to get {nameof(MobileParty)} with {mobilePartyId}";
        }


        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        mobileParty.Army = army;

        stringBuilder.AppendLine($"Added {mobileParty.Name} to {armyId}");

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_remove Army_Created_1 lord_1_3_party_1
    /// <summary>
    /// Add a Mobile Party to an Army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("mobile_party_remove", "coop.debug.army")]
    public static string RemoveMobileParty(List<string> args)
    {

        var stringBuilder = new StringBuilder();


        if (args.Count != 2)
        {

            return "Usage: coop.debug.army.mobile_party_remove <ArmyId> <MobilePartyId>";
        }

        string armyId = args[0];
        string mobilePartyId = args[1];


        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
        {
            return $"Unable to get {nameof(MobileParty)} with {mobilePartyId}";
        }


        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        army.OnRemovePartyInternal(mobileParty); 

        stringBuilder.AppendLine($"Removed {mobileParty.Name} from {armyId}");

        return stringBuilder.ToString();
    }
    // coop.debug.army.info Army_Created_1 
    /// <summary>
    /// Info about army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("info", "coop.debug.army")]
    public static string Info(List<string> args)
    {
        var sb = new StringBuilder();
        if (args.Count != 1)
        {

            return "Usage: coop.debug.army.info <ArmyId>";
        }
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }
        if (objectManager.TryGetObject<Army>(args[0], out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {args[0]}";
        }
        sb.AppendLine($"AttachedParties count: {army?.LeaderParty.AttachedParties?.Count}");
        sb.AppendLine($"{army._parties.Count}");
        sb.AppendLine($"LeaderHero: {army?.LeaderParty?.LeaderHero?.Name}");
        sb.AppendLine($"Army.name {army.Name}");
        sb.AppendLine($"Armyowner {army.ArmyOwner.Name}");
        sb.AppendLine($"leaderparty owner {army?.LeaderParty.Owner.Name}");
        return sb.ToString();
        }
    }
