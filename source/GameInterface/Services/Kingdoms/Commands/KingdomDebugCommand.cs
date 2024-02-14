using Autofac;
using Common.Extensions;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
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
    private static readonly Func<Kingdom, MBList<KingdomDecision>> GetUnresolvedDecisions = typeof(Kingdom).GetField("_unresolvedDecisions", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Kingdom, MBList<KingdomDecision>>();
    private static readonly Dictionary<string, KingdomDecisionDelegate> TryGetKingdomDecisionFunc = new Dictionary<string, KingdomDecisionDelegate>()
        {
            { nameof(DeclareWarDecision), TryGetDeclareWarDecision },
            { nameof(ExpelClanFromKingdomDecision), TryGetExpelClanFromKingdomDecision },
            { nameof(KingSelectionKingdomDecision), TryGetKingSelectionKingdomDecision },
            { nameof(KingdomPolicyDecision), TryGetKingdomPolicyDecision },
            { nameof(SettlementClaimantDecision), TryGetSettlementClaimantDecision },
            { nameof(SettlementClaimantPreliminaryDecision), TryGetSettlementClaimantPreliminaryDecision },
            { nameof(MakePeaceKingdomDecision), TryGetMakePeaceKingdomDecision },
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

        var decisions = GetUnresolvedDecisions(kingdom);
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
    private static bool TryGetMakePeaceKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 7)
        {
            kingdomDecision = null;
            message = AddMakePeaceKingdomDecisionUsage;
            return false;
        }

        string factionId = args[4];
        string dailyTribute = args[5];
        string applyResults = args[6];

        if (!objectManager.TryGetObject(factionId, out Kingdom kingdom) & !objectManager.TryGetObject(factionId, out Clan clan))
        {
            kingdomDecision = null;
            message = $"Argument5: Faction is not found by Id: {factionId}";
            return false;
        }

        IFaction faction;
        if (kingdom != null)
        {
            faction = kingdom;
        }
        else
        {
            faction = clan;
        }

        if (!int.TryParse(dailyTribute, out int dailyTributeToBePaid))
        {
            kingdomDecision = null;
            message = $"Argument6: The given value is not an integer value: {dailyTribute}";
            return false;
        }

        if (!bool.TryParse(applyResults, out bool applyResult))
        {
            kingdomDecision = null;
            message = $"Argument7: The given value is not a boolean value: {applyResults}";
            return false;
        }

        kingdomDecision = new MakePeaceKingdomDecision(proposerClan, faction, dailyTributeToBePaid, applyResult);
        message = string.Empty;
        return true;
    }
}
