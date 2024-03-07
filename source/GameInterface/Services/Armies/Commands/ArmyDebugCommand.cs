using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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

        if (ContainerProvider.TryResolve<ArmyRegistry>(out var armyRegistry) == false)
        {
            return $"Unable to resolve {nameof(ArmyRegistry)}";
        }

        foreach (var kvp in armyRegistry)
        {
            stringBuilder.Append(string.Format("Name: '{0}'\nStringId: '{1}'\n", kvp.Value.Name, kvp.Key));
        }

        return stringBuilder.ToString();
    }

    // coop.debug.army.create empire town_V1 lord_1_1 Patrolling
    /// <summary>
    /// Creates a new army on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("create", "coop.debug.army")]
    public static string CreateArmy(List<string> args)
    {
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

        var tcs = new TaskCompletionSource<string>();

        MessageBroker.Instance.Subscribe<ArmyCreated>((msg) =>
        {
            tcs.SetResult(msg.What.Data.StringId);
        });

        kingdom.CreateArmy(armyLeader, targetSettlment, armyType);

        return $"New army created with id: {tcs.Task.Result}";
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

    // coop.debug.army.destroy CoopArmy_1 NotEnoughParty
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
        if (objectManager.TryGetNonMBObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        var disbandArmyReason = args[1];
        if (Enum.TryParse(disbandArmyReason, true, out ArmyDispersionReason reason) == false)
        {
            return $"Unable to cast {disbandArmyReason} to {nameof(ArmyDispersionReason)}\n" +
                GetArmyDispersionReasonUsage();
        }

        army.DisperseInternal(reason);

        return $"Destroyed army {army.Name} with id {armyId}";
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

    // coop.debug.army.mobile_party_list CoopArmy_1
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

        if (objectManager.TryGetNonMBObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        foreach (var mobileParty in army.Parties)
        {
            stringBuilder.AppendLine($"Name: {mobileParty.Name}\nStringId: {mobileParty.StringId}");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_add CoopArmy_1 lord_1_34_party_1
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


        if (objectManager.TryGetNonMBObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        army.OnAddPartyInternal(mobileParty);
        
        stringBuilder.AppendLine($"Added {mobileParty.Name} to {armyId}");
        
        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_remove CoopArmy_1 lord_1_34_party_1
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


        if (objectManager.TryGetNonMBObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        army.OnRemovePartyInternal(mobileParty);
        
        stringBuilder.AppendLine($"Removed {mobileParty.Name} from {armyId}");
        
        return stringBuilder.ToString();
    }


}
