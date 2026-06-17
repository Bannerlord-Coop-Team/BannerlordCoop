
using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Audit;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

internal class MobilePartyDebugCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyDebugCommand>();

    [CommandLineArgumentFunction("info", "coop.debug.mobileparty")]
    public static string Info(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.mobileparty.info <PartyStringID>";
        }

        MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);

        if (mobileParty == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"MobileParty info for: {SafeToString(mobileParty)}");
        stringBuilder.AppendLine($"StringID: {SafeToString(mobileParty.StringId)}");
        stringBuilder.AppendLine($"Name: {SafeToString(mobileParty.Name)}");
        stringBuilder.AppendLine();

        stringBuilder.AppendLine("Fields:");
        AppendFields(stringBuilder, mobileParty);

        var partyResult = stringBuilder.ToString();

        stringBuilder = new StringBuilder();

        AppendFields(stringBuilder, mobileParty.Party);

        var partyBaseResults = stringBuilder.ToString();

        Logger.Debug("{Party}, {PartyBase}", partyResult, partyBaseResults);

        return $"{partyResult}\n{partyBaseResults}";
    }

    private static void AppendFields(StringBuilder stringBuilder, object instance)
    {
        if (instance == null)
        {
            stringBuilder.AppendLine("<null>");
            return;
        }

        var type = instance.GetType();

        foreach (var field in GetAllInstanceFields(type))
        {
            try
            {
                object value;

                try
                {
                    value = field.GetValue(instance);
                }
                catch (Exception e)
                {
                    stringBuilder.AppendLine($"{field.Name}: <failed to get value: {e.GetType().Name}: {e.Message}>");
                    continue;
                }

                var formattedName = GetFriendlyFieldName(field);
                var formattedType = GetFriendlyTypeName(field.FieldType);
                var formattedValue = SafeToString(value);

                stringBuilder.AppendLine($"{formattedName} ({formattedType}): {formattedValue}");
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine($"{field.Name}: <failed to print field: {e.GetType().Name}: {e.Message}>");
            }
        }
    }

    private static IEnumerable<FieldInfo> GetAllInstanceFields(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        var current = type;

        while (current != null && current != typeof(object))
        {
            foreach (var field in current.GetFields(flags))
            {
                yield return field;
            }

            current = current.BaseType;
        }
    }

    private static string GetFriendlyFieldName(FieldInfo field)
    {
        // Auto-property backing field:
        // <PropertyName>k__BackingField
        if (field.Name.StartsWith("<") && field.Name.Contains(">k__BackingField"))
        {
            var endIndex = field.Name.IndexOf(">k__BackingField", StringComparison.Ordinal);
            if (endIndex > 1)
            {
                var propertyName = field.Name.Substring(1, endIndex - 1);
                return $"{field.Name} backing for property '{propertyName}'";
            }
        }

        return field.Name;
    }

    private static string SafeToString(object value)
    {
        if (value == null)
            return "<null>";

        try
        {
            return value.ToString();
        }
        catch (Exception e)
        {
            return $"<ToString failed: {e.GetType().Name}: {e.Message}>";
        }
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "<null type>";

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tickIndex = genericTypeName.IndexOf('`');

        if (tickIndex >= 0)
            genericTypeName = genericTypeName.Substring(0, tickIndex);

        var genericArguments = type.GetGenericArguments()
            .Select(GetFriendlyTypeName)
            .ToArray();

        return $"{genericTypeName}<{string.Join(", ", genericArguments)}>";
    }

    // coop.debug.mobileparty.createParty lord_1_1 town_V1
    [CommandLineArgumentFunction("createParty", "coop.debug.mobileparty")]
    public static string CreateNewParty(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Create party is only to be called on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.mobileParty.createParty <Hero.StringId> <Settlment.StringId>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        string heroStringId = args[0];
        string settlementId = args[1];

        if (objectManager.TryGetObject<Hero>(heroStringId, out var hero) == false)
        {
            return $"Unable to get {typeof(Hero)} with id: {heroStringId}";
        }

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
        {
            return $"Unable to get {typeof(Settlement)} with id: {settlementId}";
        }

        var newParty = MobilePartyHelper.SpawnLordParty(hero, settlement);

        return $"Created new {nameof(MobileParty)} with string id: {newParty.StringId}";
    }

    // coop.debug.mobileParty.destroyParty tbd
    [CommandLineArgumentFunction("destroyParty", "coop.debug.mobileparty")]
    public static string DestroyParty(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Create party is only to be called on the server";
        }
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mobileParty.destroyParty <MobileParty.StringId>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        string partyId = args[0];

        if (objectManager.TryGetObject<MobileParty>(partyId, out var party) == false)
        {
            return $"Unable to get {typeof(MobileParty)} with id: {partyId}";
        }

        // DestroyPartyAction is the destruction path synced to clients; plain
        // RemoveParty is not. A null destroyer replicates like any other.
        DestroyPartyAction.Apply(null, party);

        return $"Destroyed {nameof(MobileParty)} with string id: {partyId}";
    }

    // coop.debug.mobileparty.destroyAllBanditParties
    [CommandLineArgumentFunction("destroyAllBanditParties", "coop.debug.mobileparty")]
    public static string DestroyAllBanditParties(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Destroy all bandit parties is only to be called on the server";
        }

        var banditParties = MobileParty.All.Where(party => party.IsBandit).ToList();

        int destroyed = 0;
        int skipped = 0;
        foreach (var banditParty in banditParties)
        {
            if (banditParty.MapEvent != null)
            {
                skipped++;
                continue;
            }

            // DestroyPartyAction is the destruction path synced to clients; plain
            // RemoveParty is not. A null destroyer replicates like any other, so
            // no party needs to be credited with the kill.
            DestroyPartyAction.Apply(null, banditParty);
            destroyed++;
        }

        return $"Destroyed {destroyed} bandit parties, skipped {skipped} in active map events";
    }

    [CommandLineArgumentFunction("list", "coop.debug.mobileparty")]
    public static string ListMobileParties(List<string> args)
    {
    	StringBuilder stringBuilder = new StringBuilder();

        List<MobileParty> mobileParty = Campaign.Current.CampaignObjectManager.MobileParties.ToList();

        mobileParty.ForEach((party) =>
        {
        	stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", party.StringId, party.Name));
        });

        return stringBuilder.ToString();
	}

    // coop.debug.mobileparty.set_wage_limit_updated CoopParty 45
    /// <summary>
    /// Just to set unlimited wage change test
    /// </summary>
    /// <param name="args">mobile party and value</param>
    /// <returns>success message</returns>
    [CommandLineArgumentFunction("set_wage_limit_updated", "coop.debug.mobileparty")]
    public static string SetWagePaymentLimit(List<string> args)
    {
    	if (args.Count < 2)
        {
        	return "Usage: coop.debug.mobileparty.set_wage_limit <PartyStringID> <value>";
        }

        int newValue = 0;
        try
        {
        	newValue = int.Parse(args[1]);
		}
        catch (Exception e)
        {
        	return $"Error setting int: {e}";
		}

        MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);


        if (mobileParty == null)
        {
        	return string.Format("ID: '{0}' not found", args[0]);
        }

        var obj = new ClanFinanceExpenseItemVM(mobileParty);

        obj.OnCurrentWageLimitUpdated(newValue);
		
		return $"Successfully called OnCurrentWageLimitUpdated({newValue});";
	}


    // coop.debug.mobileparty.set_wage_unlimited CoopParty true
    /// <summary>
    /// Just to set unlimited wage change test
    /// </summary>
    /// <param name="args">mobile party and value</param>
    /// <returns>success message</returns>
	[CommandLineArgumentFunction("set_wage_unlimited", "coop.debug.mobileparty")]
	public static string SetUnlimitedWageToggle(List<string> args)
    {
    	if (args.Count < 2)
        {
        	return "Usage: coop.debug.mobileparty.set_wage_limit <PartyStringID> <value>";
        }

        bool newValue = false;
        try
        {
        	newValue = bool.Parse(args[1]);
        }
        catch (Exception e)
        {
        	return $"Error setting bool: {e}";
        }

        MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);


        if (mobileParty == null)
        {
        	return string.Format("ID: '{0}' not found", args[0]);
        }

        var obj = new ClanFinanceExpenseItemVM(mobileParty);

        obj.OnUnlimitedWageToggled(newValue);

        return $"Successfully called OnUnlimitedWageToggled({newValue});";
    }

    // coop.debug.mobileParty.audit
    [CommandLineArgumentFunction("audit", "coop.debug.mobileparty")]
    public static string AuditParties(List<string> args)
    {
        if (ContainerProvider.TryResolve<MobilePartyAuditor>(out var auditor) == false)
        {
            return $"Unable to get {nameof(MobilePartyAuditor)}";
        }

        return auditor.Audit();
    }
}
