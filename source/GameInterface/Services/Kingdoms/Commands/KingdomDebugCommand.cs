using Autofac;
using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Messages.Collections;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands;

/// <summary>
/// Commands for <see cref="Kingdom"/>
/// </summary>
public class KingdomDebugCommand
{
    private enum CollectionTarget
    {
        Armies,
        Clans,
        FiefsCache,
        HeroesCache,
        AliveLordsCache,
        DeadLordsCache,
        SettlementsCache,
        VillagesCache,
        WarPartyComponentsCache,
    }

    private enum CollectionOperation
    {
        Add,
        Remove,
    }

    private static readonly string CollectionAddUsage = "Usage: coop.debug.kingdom.collection_add <collection> <kingdomId> <valueId> | unresolvedDecisions <kingdomId> <proposerClanId> <ignoreInfluenceCost> <decisionType> <decisionTypeArgs>";
    private static readonly string CollectionRemoveUsage = "Usage: coop.debug.kingdom.collection_remove <collection> <kingdomId> <valueId> | unresolvedDecisions <kingdomId> <index>";
    private static readonly string RemoveUsage = "Usage: coop.debug.kingdom.remove_decision <kingdomId> <Index>";
    private static readonly string AddBasicUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> <decisionType> <decisionTypeArgs>";
    private static readonly string AddDeclareWarDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> DeclareWarDecision <factionId>";
    private static readonly string AddExpelClanFromKingdomDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> ExpelClanFromKingdomDecision <clanToExpelId>";
    private static readonly string AddKingSelectionKingdomDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> KingSelectionKingdomDecision <clanToExcludeId>";
    private static readonly string AddKingdomPolicyDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> KingdomPolicyDecision <policyId> <isInvertedDecision>";
    private static readonly string AddSettlementClaimantDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> SettlementClaimantDecision <settlementId> <capturerHeroId> <clanToExcludeId>";
    private static readonly string AddSettlementClaimantPreliminaryDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> SettlementClaimantPreliminaryDecision <SettlementId>";
    private static readonly string AddMakePeaceKingdomDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> MakePeaceKingdomDecision <factionId> <dailyTribute> <applyResults>";
    private delegate bool KingdomDecisionDelegate(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message);
    private static readonly Dictionary<string, KingdomDecisionDelegate> TryGetKingdomDecisionFunc = new Dictionary<string, KingdomDecisionDelegate>()
        {
            { nameof(DeclareWarDecision), TryGetDeclareWarDecision },
            { nameof(ExpelClanFromKingdomDecision), TryGetExpelClanFromKingdomDecision },
            { nameof(KingSelectionKingdomDecision), TryGetKingSelectionKingdomDecision },
            { nameof(KingdomPolicyDecision), TryGetKingdomPolicyDecision },
            { nameof(SettlementClaimantDecision), TryGetSettlementClaimantDecision },
            { nameof(SettlementClaimantPreliminaryDecision), TryGetSettlementClaimantPreliminaryDecision },
            //{ nameof(MakePeaceKingdomDecision), TryGetMakePeaceKingdomDecision },
        };


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



    // coop.debug.kingdom.list
    /// <summary>
    /// Lists all the current Kingdoms
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the kingdoms</returns>
    [CommandLineArgumentFunction("list", "coop.debug.kingdom")]
    public static string ListKingdoms(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        List<Kingdom> kingdoms = Campaign.Current.CampaignObjectManager.Kingdoms.ToList();
        kingdoms.ForEach((kingdom) =>
        {
            stringBuilder.Append(string.Format("Name: '{0}'\n Id : '{1}'\n", kingdom.Name, kingdom.StringId));
        });
        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.add_decision_usage
    /// <summary>
    /// Lists all the usages of add_decision command.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the usages</returns>
    [CommandLineArgumentFunction("add_decision_usage", "coop.debug.kingdom")]
    public static string AddDecisionUsage(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.Append($"Basic usage: {AddBasicUsage}\n");
        stringBuilder.Append($"{AddDeclareWarDecisionUsage}\n");
        stringBuilder.Append($"{AddExpelClanFromKingdomDecisionUsage}\n");
        stringBuilder.Append($"{AddKingSelectionKingdomDecisionUsage}\n");
        stringBuilder.Append($"{AddKingdomPolicyDecisionUsage}\n");
        stringBuilder.Append($"{AddSettlementClaimantDecisionUsage}\n");
        stringBuilder.Append($"{AddSettlementClaimantPreliminaryDecisionUsage}\n");
        stringBuilder.Append($"{AddMakePeaceKingdomDecisionUsage}\n");

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.remove_decision_usage
    /// <summary>
    /// Returns the usage of remove_decision command's usage.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of usage.</returns>
    [CommandLineArgumentFunction("remove_decision_usage", "coop.debug.kingdom")]
    public static string RemoveDecisionUsage(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.Append(RemoveUsage);

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.list_decisions
    /// <summary>
    /// Lists all the decisions of a specific kingdom.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the decisions of a specific kingdom</returns>
    [CommandLineArgumentFunction("list_kingdom_decisions", "coop.debug.kingdom")]
    public static string ListKingdomDecisions(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.kingdom.list_kingdom_decisions <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
        {
            return $"ID: '{args[0]}' not found";
        }

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"Kingdom decisions of Kingdom: {kingdom.Name}\n");

        int i = 1;
        foreach (KingdomDecision kingdomDecision in kingdom.UnresolvedDecisions)
        {
            stringBuilder.Append($"{i}. {kingdomDecision.GetType().Name}\n");
            i++;
        }

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.list_policies
    /// <summary>
    /// Lists the active policies of a specific kingdom. Useful for verifying that a policy change
    /// resolved on the server has replicated to clients.
    /// </summary>
    /// <param name="args">first arg : kingdomId</param>
    /// <returns>strings of all the active policies of a specific kingdom</returns>
    [CommandLineArgumentFunction("list_policies", "coop.debug.kingdom")]
    public static string ListKingdomPolicies(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.kingdom.list_policies <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
        {
            return $"ID: '{args[0]}' not found";
        }

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"Active policies of Kingdom: {kingdom.Name}\n");

        int i = 1;
        foreach (PolicyObject policy in kingdom.ActivePolicies)
        {
            stringBuilder.Append($"{i}. {policy.Name} ({policy.StringId})\n");
            i++;
        }

        if (kingdom.ActivePolicies.Count == 0)
        {
            stringBuilder.Append("(none)\n");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.collection_list clans kingdom_V1
    /// <summary>
    /// Lists one of the synced Kingdom collection caches for server/client verification.
    /// </summary>
    /// <param name="args">collection name and kingdom id</param>
    /// <returns>IDs currently present in the selected collection</returns>
    [CommandLineArgumentFunction("collection_list", "coop.debug.kingdom")]
    public static string ListKingdomCollection(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.kingdom.collection_list <collection> <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{args[1]}' not found";
        }

        var collectionName = NormalizeCollectionName(args[0]);
        if (collectionName == "activepolicies")
        {
            return FormatCollection(objectManager, args[0], kingdom.ActivePolicies.Cast<object>());
        }

        if (collectionName == "unresolveddecisions")
        {
            return ListKingdomDecisions(new List<string> { args[1] });
        }

        if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
        {
            return parseMessage;
        }

        return FormatCollection(objectManager, args[0], GetCollectionValues(kingdom, collectionType));
    }

    // coop.debug.kingdom.collection_add clans kingdom_V1 clan_1
    /// <summary>
    /// Adds an item to a synced Kingdom collection on the server and broadcasts the change to clients.
    /// </summary>
    /// <param name="args">collection name, kingdom id, and value id</param>
    /// <returns>Result of the collection add</returns>
    [CommandLineArgumentFunction("collection_add", "coop.debug.kingdom")]
    public static string AddKingdomCollectionItem(List<string> args)
    {
        return ChangeKingdomCollection(args, CollectionOperation.Add);
    }

    // coop.debug.kingdom.collection_remove clans kingdom_V1 clan_1
    /// <summary>
    /// Removes an item from a synced Kingdom collection on the server and broadcasts the change to clients.
    /// </summary>
    /// <param name="args">collection name, kingdom id, and value id</param>
    /// <returns>Result of the collection remove</returns>
    [CommandLineArgumentFunction("collection_remove", "coop.debug.kingdom")]
    public static string RemoveKingdomCollectionItem(List<string> args)
    {
        return ChangeKingdomCollection(args, CollectionOperation.Remove);
    }

    // coop.debug.kingdom.declare_war
    /// <summary>
    /// Directly declares war between two factions (run on the server). Deterministic alternative
    /// to a DeclareWarDecision, which the kingdom AI may vote against.
    /// </summary>
    /// <param name="args">first arg : faction1Id ; second arg : faction2Id</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("declare_war", "coop.debug.kingdom")]
    public static string DeclareWar(List<string> args)
    {
        if (args.Count < 2)
        {
            return "Usage: coop.debug.kingdom.declare_war <faction1Id> <faction2Id> (run on the server)";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetFaction(objectManager, args[0], out IFaction faction1) == false)
        {
            return $"Faction not found with id: {args[0]}";
        }

        if (TryGetFaction(objectManager, args[1], out IFaction faction2) == false)
        {
            return $"Faction not found with id: {args[1]}";
        }

        DeclareWarAction.ApplyByDefault(faction1, faction2);
        return $"Declared war between '{faction1.Name}' and '{faction2.Name}'.";
    }

    // coop.debug.kingdom.make_peace
    /// <summary>
    /// Directly makes peace between two factions (run on the server).
    /// </summary>
    /// <param name="args">first arg : faction1Id ; second arg : faction2Id</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("make_peace", "coop.debug.kingdom")]
    public static string MakePeace(List<string> args)
    {
        if (args.Count < 2)
        {
            return "Usage: coop.debug.kingdom.make_peace <faction1Id> <faction2Id> (run on the server)";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetFaction(objectManager, args[0], out IFaction faction1) == false)
        {
            return $"Faction not found with id: {args[0]}";
        }

        if (TryGetFaction(objectManager, args[1], out IFaction faction2) == false)
        {
            return $"Faction not found with id: {args[1]}";
        }

        MakePeaceAction.Apply(faction1, faction2);
        return $"Made peace between '{faction1.Name}' and '{faction2.Name}'.";
    }

    private static string ChangeKingdomCollection(List<string> args, CollectionOperation operation)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count < 3)
        {
            return operation == CollectionOperation.Add ? CollectionAddUsage : CollectionRemoveUsage;
        }

        var collectionName = NormalizeCollectionName(args[0]);
        if (collectionName == "activepolicies")
        {
            return ChangeActivePolicy(args, operation);
        }

        if (collectionName == "unresolveddecisions")
        {
            var forwardedArgs = args.Skip(1).ToList();
            return operation == CollectionOperation.Add
                ? AddDecision(forwardedArgs)
                : RemoveDecision(forwardedArgs);
        }

        if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
        {
            return parseMessage;
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{args[1]}' not found";
        }

        if (!TryResolveCollectionValue(objectManager, collectionType, args[2], out var value, out var resolveMessage))
        {
            return resolveMessage;
        }

        using (new AllowedThread())
        {
            ApplyCollectionChange(kingdom, collectionType, operation, value);
        }

        return $"{operation} {args[2]} in {args[0]} for kingdom {args[1]}.";
    }

    private static string ChangeActivePolicy(List<string> args, CollectionOperation operation)
    {
        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{args[1]}' not found";
        }

        if (objectManager.TryGetObject(args[2], out PolicyObject policy) == false)
        {
            return $"PolicyObject with ID: '{args[2]}' not found";
        }

        if (operation == CollectionOperation.Add)
        {
            kingdom.AddPolicy(policy);
            return $"Added policy {args[2]} to kingdom {args[1]}.";
        }

        kingdom.RemovePolicy(policy);
        return $"Removed policy {args[2]} from kingdom {args[1]}.";
    }

    /// <summary>
    /// Resolves a faction id to either a Kingdom or a Clan.
    /// </summary>
    private static bool TryGetFaction(IObjectManager objectManager, string id, out IFaction faction)
    {
        if (objectManager.TryGetObject(id, out Kingdom kingdom))
        {
            faction = kingdom;
            return true;
        }
        if (objectManager.TryGetObject(id, out Clan clan))
        {
            faction = clan;
            return true;
        }
        faction = null;
        return false;
    }

    private static bool TryParseCollectionTarget(
        string value,
        out CollectionTarget collectionType,
        out string message)
    {
        collectionType = default;
        message = string.Empty;

        switch (NormalizeCollectionName(value))
        {
            case "armies":
                collectionType = CollectionTarget.Armies;
                return true;
            case "clans":
                collectionType = CollectionTarget.Clans;
                return true;
            case "fiefscache":
                collectionType = CollectionTarget.FiefsCache;
                return true;
            case "heroescache":
                collectionType = CollectionTarget.HeroesCache;
                return true;
            case "lordscache":
            case "alivelordscache":
                collectionType = CollectionTarget.AliveLordsCache;
                return true;
            case "deadlordscache":
                collectionType = CollectionTarget.DeadLordsCache;
                return true;
            case "settlementscache":
                collectionType = CollectionTarget.SettlementsCache;
                return true;
            case "villagescache":
                collectionType = CollectionTarget.VillagesCache;
                return true;
            case "warpartycomponentscache":
                collectionType = CollectionTarget.WarPartyComponentsCache;
                return true;
            default:
                message = "Unknown collection. Valid values: activePolicies, armies, clans, fiefsCache, heroesCache, lordsCache, aliveLordsCache, deadLordsCache, settlementsCache, unresolvedDecisions, villagesCache, warPartyComponentsCache.";
                return false;
        }
    }

    private static string NormalizeCollectionName(string value)
    {
        return value.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static IEnumerable<object> GetCollectionValues(Kingdom kingdom, CollectionTarget collectionType)
    {
        return collectionType switch
        {
            CollectionTarget.Armies => kingdom._armies?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.Clans => kingdom._clans?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.FiefsCache => kingdom._fiefsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.HeroesCache => kingdom._heroesCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.AliveLordsCache => kingdom._aliveLordsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.DeadLordsCache => kingdom._deadLordsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.SettlementsCache => kingdom._settlementsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.VillagesCache => kingdom._villagesCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.WarPartyComponentsCache => kingdom._warPartyComponentsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            _ => Enumerable.Empty<object>(),
        };
    }

    private static void ApplyCollectionChange(
        Kingdom kingdom,
        CollectionTarget collectionType,
        CollectionOperation operation,
        object value)
    {
        switch (collectionType)
        {
            case CollectionTarget.Armies:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new ArmyListUpdated(kingdom, (Army)value));
                    KingdomCollectionHandler.ApplyArmyListUpdate(kingdom, (Army)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new ArmyListRemoved(kingdom, (Army)value));
                    KingdomCollectionHandler.ApplyArmyListRemove(kingdom, (Army)value);
                }
                break;
            case CollectionTarget.Clans:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new ClanListUpdated(kingdom, (Clan)value));
                    KingdomCollectionHandler.ApplyClanListUpdate(kingdom, (Clan)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new ClanListRemoved(kingdom, (Clan)value));
                    KingdomCollectionHandler.ApplyClanListRemove(kingdom, (Clan)value);
                }
                break;
            case CollectionTarget.FiefsCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new FiefsCacheUpdated(kingdom, (Town)value));
                    KingdomCollectionHandler.ApplyFiefsCacheUpdate(kingdom, (Town)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new FiefsCacheRemoved(kingdom, (Town)value));
                    KingdomCollectionHandler.ApplyFiefsCacheRemove(kingdom, (Town)value);
                }
                break;
            case CollectionTarget.HeroesCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new HeroesCacheUpdated(kingdom, (Hero)value));
                    KingdomCollectionHandler.ApplyHeroesCacheUpdate(kingdom, (Hero)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new HeroesCacheRemoved(kingdom, (Hero)value));
                    KingdomCollectionHandler.ApplyHeroesCacheRemove(kingdom, (Hero)value);
                }
                break;
            case CollectionTarget.AliveLordsCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new AliveLordsCacheUpdated(kingdom, (Hero)value));
                    KingdomCollectionHandler.ApplyAliveLordsCacheUpdate(kingdom, (Hero)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new AliveLordsCacheRemoved(kingdom, (Hero)value));
                    KingdomCollectionHandler.ApplyAliveLordsCacheRemove(kingdom, (Hero)value);
                }
                break;
            case CollectionTarget.DeadLordsCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new DeadLordsCacheUpdated(kingdom, (Hero)value));
                    KingdomCollectionHandler.ApplyDeadLordsCacheUpdate(kingdom, (Hero)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new DeadLordsCacheRemoved(kingdom, (Hero)value));
                    KingdomCollectionHandler.ApplyDeadLordsCacheRemove(kingdom, (Hero)value);
                }
                break;
            case CollectionTarget.SettlementsCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new SettlementsCacheUpdated(kingdom, (Settlement)value));
                    KingdomCollectionHandler.ApplySettlementsCacheUpdate(kingdom, (Settlement)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new SettlementsCacheRemoved(kingdom, (Settlement)value));
                    KingdomCollectionHandler.ApplySettlementsCacheRemove(kingdom, (Settlement)value);
                }
                break;
            case CollectionTarget.VillagesCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new VillagesCacheUpdated(kingdom, (Village)value));
                    KingdomCollectionHandler.ApplyVillagesCacheUpdate(kingdom, (Village)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new VillagesCacheRemoved(kingdom, (Village)value));
                    KingdomCollectionHandler.ApplyVillagesCacheRemove(kingdom, (Village)value);
                }
                break;
            case CollectionTarget.WarPartyComponentsCache:
                if (operation == CollectionOperation.Add)
                {
                    MessageBroker.Instance.Publish(kingdom, new WarPartyComponentsCacheUpdated(kingdom, (WarPartyComponent)value));
                    KingdomCollectionHandler.ApplyWarPartyComponentsCacheUpdate(kingdom, (WarPartyComponent)value);
                }
                else
                {
                    MessageBroker.Instance.Publish(kingdom, new WarPartyComponentsCacheRemoved(kingdom, (WarPartyComponent)value));
                    KingdomCollectionHandler.ApplyWarPartyComponentsCacheRemove(kingdom, (WarPartyComponent)value);
                }
                break;
        }
    }

    private static string FormatCollection(
        IObjectManager objectManager,
        string collectionName,
        IEnumerable<object> values)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"{collectionName}:");

        var count = 0;
        foreach (var value in values)
        {
            count++;
            if (objectManager.TryGetId(value, out var id))
            {
                stringBuilder.AppendLine($"{count}. {id}");
            }
            else
            {
                stringBuilder.AppendLine($"{count}. {value?.GetType().Name ?? "<null>"}");
            }
        }

        if (count == 0)
        {
            stringBuilder.AppendLine("(none)");
        }

        return stringBuilder.ToString();
    }

    private static bool TryResolveCollectionValue(
        IObjectManager objectManager,
        CollectionTarget collectionType,
        string valueId,
        out object value,
        out string message)
    {
        value = null;
        message = string.Empty;

        switch (collectionType)
        {
            case CollectionTarget.Armies:
                return TryResolve<Army>(objectManager, valueId, out value, out message);
            case CollectionTarget.Clans:
                return TryResolve<Clan>(objectManager, valueId, out value, out message);
            case CollectionTarget.FiefsCache:
                return TryResolve<Town>(objectManager, valueId, out value, out message);
            case CollectionTarget.HeroesCache:
            case CollectionTarget.AliveLordsCache:
            case CollectionTarget.DeadLordsCache:
                return TryResolve<Hero>(objectManager, valueId, out value, out message);
            case CollectionTarget.SettlementsCache:
                return TryResolve<Settlement>(objectManager, valueId, out value, out message);
            case CollectionTarget.VillagesCache:
                return TryResolve<Village>(objectManager, valueId, out value, out message);
            case CollectionTarget.WarPartyComponentsCache:
                return TryResolve<WarPartyComponent>(objectManager, valueId, out value, out message);
            default:
                message = $"Unsupported collection {collectionType}.";
                return false;
        }
    }

    private static bool TryResolve<T>(
        IObjectManager objectManager,
        string valueId,
        out object value,
        out string message)
    {
        value = null;

        if (objectManager.TryGetObject(valueId, out T resolved) == false)
        {
            message = $"{typeof(T).Name} with ID: '{valueId}' not found";
            return false;
        }

        value = resolved;
        message = string.Empty;
        return true;
    }

    // coop.debug..kingdom.add_decision
    /// <summary>
    /// Adds a decision to a Kingdom.
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : decision to add</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("add_decision", "coop.debug.kingdom")]
    public static string AddDecision(List<string> args)
    {
        if (args.Count < 4)
        {
            return AddBasicUsage;
        }

        string kingdomId = args[0];
        string clanId = args[1];
        string ignoreInfluence = args[2];
        string decisionType = args[3];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }
        if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{kingdomId}' not found";
        }

        if (objectManager.TryGetObject(clanId, out Clan proposerClan) == false)
        {
            return $"Clan with ID: '{clanId}' not found";
        }

        if (!bool.TryParse(ignoreInfluence, out bool ignoreInfluenceCost))
        {
            return $"Couldnt convert ignoreInfluenceCost: {ignoreInfluence}";
        }

        if (!TryGetKingdomDecisionFunc.ContainsKey(decisionType))
        {
            return $"Kingdom decision type: {decisionType} does not exist.";
        }

        if (!TryGetKingdomDecisionFunc[decisionType](objectManager, args, proposerClan, out KingdomDecision kingdomDecision, out string message))
        {
            return message;
        }

        kingdom.AddDecision(kingdomDecision, ignoreInfluenceCost);
        return $"Kingdom decision added successfully.";
    }

    // coop.debug.kingdom.remove_decision
    /// <summary>
    /// Removes a decision from a Kingdom
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : index of decision to remove</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("remove_decision", "coop.debug.kingdom")]
    public static string RemoveDecision(List<string> args)
    {
        if (args.Count != 2)
        {
            return RemoveUsage;
        }

        string kingdomId = args[0];
        string index = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }
        if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{kingdomId}' not found";
        }

        if (!int.TryParse(index, out int idx))
        {
            return $"Argument2: {index} is not a number.";
        }

        var decisions = kingdom._unresolvedDecisions;
        if (idx > 0 && idx <= decisions.Count)
        {
            kingdom.RemoveDecision(decisions[idx - 1]);
        }
        else
        {
            return "Index is out of bounds.";
        }

        return $"Kingdom decision removed.";
    }

    /// <summary>
    /// Tries getting declare war decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetDeclareWarDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddDeclareWarDecisionUsage;
            return false;
        }

        string factionId = args[4];
        if (!objectManager.TryGetObject(factionId, out Kingdom kingdom) & !objectManager.TryGetObject(factionId, out Clan clanFaction))
        {
            kingdomDecision = null;
            message = $"Argument5: Faction is not found with id: {factionId}.";
            return false;
        }

        IFaction faction;
        if (kingdom != null)
        {
            faction = kingdom;
        }
        else
        {
            faction = clanFaction;
        }

        kingdomDecision = new DeclareWarDecision(proposerClan, faction);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting expel clan from kingdom decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetExpelClanFromKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddExpelClanFromKingdomDecisionUsage;
            return false;
        }

        string clanId = args[4];
        if (!objectManager.TryGetObject(clanId, out Clan clan))
        {
            kingdomDecision = null;
            message = $"Argument5: Clan not found with id: {clanId}";
            return false;
        }
        kingdomDecision = new ExpelClanFromKingdomDecision(proposerClan, clan);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting king selection kingdom decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetKingSelectionKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddKingSelectionKingdomDecisionUsage;
            return false;
        }

        string clanId = args[4];
        if (!objectManager.TryGetObject(clanId, out Clan clan))
        {
            kingdomDecision = null;
            message = $"Argument5: Clan not found with id: {clanId}";
            return false;
        }
        kingdomDecision = new KingSelectionKingdomDecision(proposerClan, clan);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting kingdom policy decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetKingdomPolicyDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 6)
        {
            kingdomDecision = null;
            message = AddKingdomPolicyDecisionUsage;
            return false;
        }

        string policyId = args[4];
        string isInvertedDecision = args[5];

        if (!objectManager.TryGetObject(policyId, out PolicyObject policy))
        {
            kingdomDecision = null;
            message = $"Argument5: PolicyObject not found by id: {policyId}";
            return false;
        }

        if (!bool.TryParse(isInvertedDecision, out bool isInverted))
        {
            kingdomDecision = null;
            message = $"Argument6: The given value is not a boolean value: {isInvertedDecision}";
            return false;
        }

        kingdomDecision = new KingdomPolicyDecision(proposerClan, policy, isInverted);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting settlement claimant decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetSettlementClaimantDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 7)
        {
            kingdomDecision = null;
            message = AddSettlementClaimantDecisionUsage;
            return false;
        }

        string settlementId = args[4];
        string capturerHeroId = args[5];
        string clanToExcludeId = args[6];

        if (!objectManager.TryGetObject(settlementId, out Settlement settlement))
        {
            kingdomDecision = null;
            message = $"Argument5: Settlement not found by id: {settlementId}";
            return false;
        }

        if (!objectManager.TryGetObject(capturerHeroId, out Hero capturerHero))
        {
            kingdomDecision = null;
            message = $"Argument6: Hero not found by id: {capturerHeroId}";
            return false;
        }
        if (!objectManager.TryGetObject(clanToExcludeId, out Clan clanToExclude))
        {
            kingdomDecision = null;
            message = $"Argument7: Clan not found by id: {clanToExcludeId}";
            return false;
        }

        kingdomDecision = new SettlementClaimantDecision(proposerClan, settlement, capturerHero, clanToExclude);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting settlement claimant preliminary war decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetSettlementClaimantPreliminaryDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddSettlementClaimantPreliminaryDecisionUsage;
            return false;
        }
        string settlementId = args[4];

        if (!objectManager.TryGetObject(settlementId, out Settlement settlement))
        {
            kingdomDecision = null;
            message = $"Argument5: Settlement not found by id: {settlementId}";
            return false;
        }

        kingdomDecision = new SettlementClaimantPreliminaryDecision(proposerClan, settlement);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting make peace decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>

    //private static bool TryGetMakePeaceKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    //{
    //    if (args.Count < 7)
    //    {
    //        kingdomDecision = null;
    //        message = AddMakePeaceKingdomDecisionUsage;
    //        return false;
    //    }

    //    string factionId = args[4];
    //    string dailyTribute = args[5];
    //    string applyResults = args[6];

    //    if (!objectManager.TryGetObject(factionId, out Kingdom kingdom) & !objectManager.TryGetObject(factionId, out Clan clan))
    //    {
    //        kingdomDecision = null;
    //        message = $"Argument5: Faction is not found by Id: {factionId}";
    //        return false;
    //    }

    //    IFaction faction;
    //    if (kingdom != null)
    //    {
    //        faction = kingdom;
    //    }
    //    else
    //    {
    //        faction = clan;
    //    }

    //    if (!int.TryParse(dailyTribute, out int dailyTributeToBePaid))
    //    {
    //        kingdomDecision = null;
    //        message = $"Argument6: The given value is not an integer value: {dailyTribute}";
    //        return false;
    //    }

    //    if (!bool.TryParse(applyResults, out bool applyResult))
    //    {
    //        kingdomDecision = null;
    //        message = $"Argument7: The given value is not a boolean value: {applyResults}";
    //        return false;
    //    }

    //    kingdomDecision = new MakePeaceKingdomDecision(proposerClan, faction, dailyTributeToBePaid, applyResult);
    //    message = string.Empty;
    //    return true;
    //}
}
