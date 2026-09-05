using Common.Commands;
using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Audit;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
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
namespace GameInterface.Services.MobileParties.Commands;

internal class MobilePartyDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyDebugCommand>();

    public sealed class InfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "info";

        public string Description => "Shows the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyStringId", "The party string id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);

            if (mobileParty == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
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

            return Succeeded($"{partyResult}\n{partyBaseResults}");

        }
    }

    // coop.debug.mobileparty.component_info <PartyStringID>
    // Dumps the party's _partyComponent fields (LordPartyComponent/Caravan/Garrison/etc.), which the plain
    // info cheat does NOT show (it dumps MobileParty + PartyBase only). e.g. LordPartyComponent._wagePaymentLimit.
    public sealed class ComponentInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "component_info";

        public string Description => "Runs info for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyStringId", "The party string id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);
            if (mobileParty == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"PartyComponent ({mobileParty.PartyComponent?.GetType().Name ?? "null"}) for: {SafeToString(mobileParty.Name)}");
            AppendFields(stringBuilder, mobileParty.PartyComponent);
            return Succeeded(stringBuilder.ToString());

        }
    }

    // coop.debug.mobileparty.attachment_ids <PartyStringID>
    // Prints the network ObjectManager id THIS machine holds for a party and each of its non-MBObjectBase
    // attachments. Run on the server and on each client and compare: a party the client got via live create
    // matches the server's runtime "Created_N"/concrete-type ids, while a party re-derived at join carries
    // "{Type}_{StringId}" ids that never reconcile with the server's, so its synced updates fail to resolve.
    public sealed class AttachmentIdsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "attachment_ids";

        public string Description => "Runs ids for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyStringId", "The party string id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);
            if (mobileParty == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
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
            return Succeeded(result);

        }
    }

    public sealed class VerifyAiAuthorityCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "verify_ai_authority";

        public string Description => "Verifies ai authority for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mobilePartyId", "The mobile party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("verify_ai_authority is server-only");
            }


            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            {
                return Failed($"Unable to get {nameof(IPlayerManager)}");
            }

            if (!objectManager.TryGetObjectWithLogging<MobileParty>(args[0], out var mobileParty))
            {
                return Failed($"Unable to get {nameof(MobileParty)} with id: {args[0]}");
            }

            if (!playerManager.Contains(mobileParty))
            {
                return Failed($"Party {args[0]} is not registered as a player party");
            }

            var partyAi = mobileParty.Ai;
            if (partyAi == null)
            {
                return Failed($"Party {args[0]} has no {nameof(MobilePartyAi)}");
            }

            bool previousNeedsUpdate = partyAi.DefaultBehaviorNeedsUpdate;
            bool tickWasBlocked;
            try
            {
                partyAi.DefaultBehaviorNeedsUpdate = true;
                partyAi.Tick(0f);
                tickWasBlocked = partyAi.DefaultBehaviorNeedsUpdate;
            }
            finally
            {
                partyAi.DefaultBehaviorNeedsUpdate = previousNeedsUpdate;
            }

            return Succeeded(tickWasBlocked
                ? $"Server AI tick blocked for player party {args[0]}"
                : $"Server AI tick ran for player party {args[0]}");

        }
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

    // coop.debug.mobileparty.create_party lord_1_1 town_V1
    public sealed class CreateNewPartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "create_party";

        public string Description => "Creates party for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("heroId", "The hero id."),
            new ExpectedArgs("settlementId", "The settlement id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Create party is only to be called on the server");
            }


            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            string heroStringId = args[0];
            string settlementId = args[1];

            if (objectManager.TryGetObject<Hero>(heroStringId, out var hero) == false)
            {
                return Failed($"Unable to get {typeof(Hero)} with id: {heroStringId}");
            }

            if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            {
                return Failed($"Unable to get {typeof(Settlement)} with id: {settlementId}");
            }

            var newParty = MobilePartyHelper.SpawnLordParty(hero, settlement);

            return Succeeded($"Created new {nameof(MobileParty)} with string id: {newParty.StringId}");

        }
    }

    // coop.debug.mobileparty.spawn_test_parties [count] [settlementId]
    // Server-only. Spawns N lord parties from currently party-less lords near the settlement
    // (default Danustica, town_ES1) to exercise mid-session party creation/replication to clients.
    public sealed class SpawnTestPartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "spawn_test_parties";

        public string Description => "Spawns test parties for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("count", "The count.", isRequired: false),
            new ExpectedArgs("settlementId", "The settlement id.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("spawn_test_parties is server-only");
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
                return Failed($"Settlement '{settlementId}' not found. Try one of: {towns}");
            }

            var candidates = Hero.AllAliveHeroes
                .Where(h => h != Hero.MainHero && h.Clan != null && !h.IsPrisoner && !h.IsChild
                            && h.IsLord && h.PartyBelongedTo == null)
                .Take(count)
                .ToList();

            if (candidates.Count == 0)
            {
                return Failed("No party-less lords available to spawn");
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

            return Succeeded($"Spawned {spawned} test parties near {settlement.Name}:\n{sb}");

        }
    }

    // coop.debug.mobileParty.destroyParty tbd
    public sealed class DestroyPartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "destroy_party";

        public string Description => "Destroys party for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mobilePartyId", "The mobile party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Create party is only to be called on the server");
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            string partyId = args[0];

            if (objectManager.TryGetObject<MobileParty>(partyId, out var party) == false)
            {
                return Failed($"Unable to get {typeof(MobileParty)} with id: {partyId}");
            }

            // DestroyPartyAction is the destruction path synced to clients; plain
            // RemoveParty is not. A null destroyer replicates like any other.
            DestroyPartyAction.Apply(null, party);

            return Succeeded($"Destroyed {nameof(MobileParty)} with string id: {partyId}");

        }
    }

    // coop.debug.mobileparty.destroy_all_bandit_parties
    public sealed class DestroyAllBanditPartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "destroy_all_bandit_parties";

        public string Description => "Destroys all bandit parties for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Destroy all bandit parties is only to be called on the server");
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

            return Succeeded($"Destroyed {destroyed} bandit parties, skipped {skipped} in active map events");

        }
    }

    public sealed class ListMobilePartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "list";

        public string Description => "Lists the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<MobileParty> mobileParty = Campaign.Current.CampaignObjectManager.MobileParties.ToList();

            mobileParty.ForEach((party) =>
            {
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", party.StringId, party.Name));
            });

            return Succeeded(stringBuilder.ToString());

        }
    }

    // coop.debug.mobileparty.set_wage_limit_updated CoopParty 45
    /// <summary>
    /// Just to set unlimited wage change test
    /// </summary>
    /// <param name="args">mobile party and value</param>
    /// <returns>success message</returns>
    public sealed class SetWagePaymentLimitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "set_wage_limit_updated";

        public string Description => "Sets wage limit updated for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyStringId", "The party string id."),
            new ExpectedArgs("value", "The value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            int newValue = 0;
            try
            {
                newValue = int.Parse(args[1]);
            }
            catch (Exception e)
            {
                return Failed($"Error setting int: {e}");
            }

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);


            if (mobileParty == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            var obj = new ClanFinanceExpenseItemVM(mobileParty);

            obj.OnCurrentWageLimitUpdated(newValue);

            return Succeeded($"Successfully called OnCurrentWageLimitUpdated({newValue});");

        }
    }


    // coop.debug.mobileparty.set_wage_unlimited CoopParty true
    /// <summary>
    /// Just to set unlimited wage change test
    /// </summary>
    /// <param name="args">mobile party and value</param>
    /// <returns>success message</returns>
    public sealed class SetUnlimitedWageToggleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "set_wage_unlimited";

        public string Description => "Sets wage unlimited for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyStringId", "The party string id."),
            new ExpectedArgs("value", "The value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            bool newValue = false;
            try
            {
                newValue = bool.Parse(args[1]);
            }
            catch (Exception e)
            {
                return Failed($"Error setting bool: {e}");
            }

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);


            if (mobileParty == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            var obj = new ClanFinanceExpenseItemVM(mobileParty);

            obj.OnUnlimitedWageToggled(newValue);

            return Succeeded($"Successfully called OnUnlimitedWageToggled({newValue});");

        }
    }

    // coop.debug.mobileParty.audit
    public sealed class AuditPartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "audit";

        public string Description => "Audits the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<MobilePartyAuditor>(out var auditor) == false)
            {
                return Failed($"Unable to get {nameof(MobilePartyAuditor)}");
            }

            return Succeeded(auditor.Audit());

        }
    }
}
