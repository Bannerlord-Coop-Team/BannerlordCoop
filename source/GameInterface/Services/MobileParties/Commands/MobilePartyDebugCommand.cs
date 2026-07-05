
using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Audit;
using GameInterface.Services.ObjectManager;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
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
        stringBuilder.AppendLine($"Morale: {mobileParty.Morale}");
        stringBuilder.AppendLine($"RecentEventsMorale: {mobileParty.RecentEventsMorale}");
        stringBuilder.AppendLine($"HasUnpaidWages: {mobileParty.HasUnpaidWages}");
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

    // coop.debug.mobileparty.component_info <PartyStringID>
    // Dumps the party's _partyComponent fields (LordPartyComponent/Caravan/Garrison/etc.), which the plain
    // info cheat does NOT show (it dumps MobileParty + PartyBase only). e.g. LordPartyComponent._wagePaymentLimit.
    [CommandLineArgumentFunction("component_info", "coop.debug.mobileparty")]
    public static string ComponentInfo(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.mobileparty.component_info <PartyStringID>";
        }

        MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);
        if (mobileParty == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"PartyComponent ({mobileParty.PartyComponent?.GetType().Name ?? "null"}) for: {SafeToString(mobileParty.Name)}");
        AppendFields(stringBuilder, mobileParty.PartyComponent);
        return stringBuilder.ToString();
    }

    // coop.debug.mobileparty.attachment_ids <PartyStringID>
    // Prints the network ObjectManager id THIS machine holds for a party and each of its non-MBObjectBase
    // attachments. Run on the server and on each client and compare: a party the client got via live create
    // matches the server's runtime "Created_N"/concrete-type ids, while a party re-derived at join carries
    // "{Type}_{StringId}" ids that never reconcile with the server's, so its synced updates fail to resolve.
    [CommandLineArgumentFunction("attachment_ids", "coop.debug.mobileparty")]
    public static string AttachmentIds(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.mobileparty.attachment_ids <PartyStringID>";
        }

        MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);
        if (mobileParty == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var party = mobileParty.Party;

        var sb = new StringBuilder();
        sb.AppendLine($"Attachment ids on {(ModInformation.IsServer ? "SERVER" : "CLIENT")} for {SafeToString(mobileParty.Name)} (StringId {mobileParty.StringId}):");
        AppendAttachmentId(sb, objectManager, "MobileParty", mobileParty);
        AppendAttachmentId(sb, objectManager, "PartyBase", party);
        AppendAttachmentId(sb, objectManager, "MemberRoster", party?.MemberRoster);
        AppendAttachmentId(sb, objectManager, "PrisonRoster", party?.PrisonRoster);
        AppendAttachmentId(sb, objectManager, "ItemRoster", party?.ItemRoster);
        AppendAttachmentId(sb, objectManager, "PartyComponent", mobileParty.PartyComponent);

        var result = sb.ToString();
        Logger.Debug("{AttachmentIds}", result);
        return result;
    }

    private static void AppendAttachmentId(StringBuilder sb, IObjectManager objectManager, string label, object obj)
    {
        if (obj == null)
        {
            sb.AppendLine($"  {label}: <null>");
            return;
        }

        var id = objectManager.TryGetId(obj, out var foundId) ? foundId : "NOT REGISTERED on this machine";
        sb.AppendLine($"  {label} ({obj.GetType().Name}): {id}");
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

    // coop.debug.mobileparty.spawn_test_parties [count] [settlementId]
    // Server-only. Spawns N lord parties from currently party-less lords near the settlement
    // (default Danustica, town_ES1) to exercise mid-session party creation/replication to clients.
    [CommandLineArgumentFunction("spawn_test_parties", "coop.debug.mobileparty")]
    public static string SpawnTestParties(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "spawn_test_parties is server-only";
        }

        int count = 5;
        if (args.Count >= 1 && int.TryParse(args[0], out var parsed) && parsed > 0)
        {
            count = parsed;
        }

        // Spawn near a settlement (default Danustica, town_ES1 -- a common client location).
        string settlementId = args.Count >= 2 ? args[1] : "town_ES1";
        var settlement = Settlement.All.FirstOrDefault(s => s.StringId == settlementId);
        if (settlement == null)
        {
            var towns = string.Join(", ", Settlement.All.Where(s => s.IsTown).Take(15).Select(s => s.StringId));
            return $"Settlement '{settlementId}' not found. Try one of: {towns}";
        }

        var candidates = Hero.AllAliveHeroes
            .Where(h => h != Hero.MainHero && h.Clan != null && !h.IsPrisoner && !h.IsChild
                        && h.IsLord && h.PartyBelongedTo == null)
            .Take(count)
            .ToList();

        if (candidates.Count == 0)
        {
            return "No party-less lords available to spawn";
        }

        var sb = new StringBuilder();
        int spawned = 0;
        foreach (var hero in candidates)
        {
            try
            {
                var party = MobilePartyHelper.SpawnLordParty(hero, settlement);
                sb.AppendLine($"Spawned {party.StringId} for {hero.Name} at {settlement.Name} ({party.MemberRoster.TotalManCount} troops)");
                spawned++;
            }
            catch (Exception e)
            {
                sb.AppendLine($"Failed to spawn for {hero.Name}: {e.Message}");
            }
        }

        return $"Spawned {spawned} test parties near {settlement.Name}:\n{sb}";
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
