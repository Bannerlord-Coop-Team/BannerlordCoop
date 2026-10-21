using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Utils.Commands;

// Resolves a console command argument to a game object by id first, then by display name.
public interface ICommandObjectResolver
{
    bool TryResolveHero(string query, out Hero hero, out string error);
    bool TryResolveClan(string query, out Clan clan, out string error);
    bool TryResolveKingdom(string query, out Kingdom kingdom, out string error);
    // Clan or Kingdom; a name shared by both is ambiguous.
    bool TryResolveFaction(string query, out IFaction faction, out string error);
    bool TryResolveSettlement(string query, out Settlement settlement, out string error);
    // Also matches the Town's own id (town_comp_ES1) and its Settlement id or name (town_ES1, "Danustica"); includes castles.
    bool TryResolveTown(string query, out Town town, out string error);
    // Also matches the Village's own id and its Settlement id or name.
    bool TryResolveVillage(string query, out Village village, out string error);
    bool TryResolveMobileParty(string query, out MobileParty party, out string error);
    bool TryResolveArmy(string query, out Army army, out string error);
    bool TryResolveCharacterObject(string query, out CharacterObject character, out string error);
    bool TryResolveItemObject(string query, out ItemObject item, out string error);

    // Resolves against a caller-supplied candidate list (a siege camp's parties, a settlement's locations, PolicyObject.All).
    bool TryResolve<T>(string query, IEnumerable<T> candidates, Func<T, string> nameOf, out T value, out string error) where T : class;

    // The id to print for an object in command output: its registry id, falling back to StringId.
    string DisplayId(object obj);
}

// Implements id-then-name command argument resolution; the matching core is game-free so it can be unit tested.
public class CommandObjectResolver : ICommandObjectResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<CommandObjectResolver>();

    private const int MaxListedCandidates = 25;

    private const string NoCampaignError = "No campaign is loaded.";

    private readonly IObjectManager objectManager;

    public CommandObjectResolver(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryResolveHero(string query, out Hero hero, out string error) =>
        ResolveGameObject("Hero", query, () => Hero.AllAliveHeroes, h => h.Name, out hero, out error);

    public bool TryResolveClan(string query, out Clan clan, out string error) =>
        ResolveGameObject("Clan", query, () => Clan.All, c => c.Name, out clan, out error);

    public bool TryResolveKingdom(string query, out Kingdom kingdom, out string error) =>
        ResolveGameObject("Kingdom", query, () => Kingdom.All, k => k.Name, out kingdom, out error);

    public bool TryResolveFaction(string query, out IFaction faction, out string error) =>
        ResolveGameObject("Faction", query, () => Clan.All.Cast<IFaction>().Concat(Kingdom.All), f => f.Name, out faction, out error);

    public bool TryResolveSettlement(string query, out Settlement settlement, out string error) =>
        ResolveGameObject("Settlement", query, () => Settlement.All, s => s.Name, out settlement, out error);

    public bool TryResolveTown(string query, out Town town, out string error) =>
        ResolveGameObject("Town", query, () => Town.AllTowns, t => t.Settlement?.Name, out town, out error, t => t.Settlement);

    public bool TryResolveVillage(string query, out Village village, out string error) =>
        ResolveGameObject("Village", query, () => Village.All, v => v.Settlement?.Name, out village, out error, v => v.Settlement);

    public bool TryResolveMobileParty(string query, out MobileParty party, out string error) =>
        ResolveGameObject("Mobile party", query, () => MobileParty.All, p => p.Name, out party, out error);

    public bool TryResolveArmy(string query, out Army army, out string error) =>
        ResolveGameObject("Army", query, () => Kingdom.All.SelectMany(k => k.Armies), a => a.Name, out army, out error);

    public bool TryResolveCharacterObject(string query, out CharacterObject character, out string error) =>
        ResolveGameObject("Character", query, () => MBObjectManager.Instance.GetObjectTypeList<CharacterObject>(), c => c.Name, out character, out error);

    public bool TryResolveItemObject(string query, out ItemObject item, out string error) =>
        ResolveGameObject("Item", query, () => MBObjectManager.Instance.GetObjectTypeList<ItemObject>(), i => i.Name, out item, out error);

    public bool TryResolve<T>(string query, IEnumerable<T> candidates, Func<T, string> nameOf, out T value, out string error) where T : class =>
        TryMatch(typeof(T).Name, query, () => candidates, LookupRegistry<T>, true, IdsOf<T>, nameOf, DisplayIdOf<T>, out value, out error);

    public string DisplayId(object obj) => obj == null ? "no id" : DisplayIdOf(obj);

    // Runs the shared core over the type's live game list; without a campaign only ids resolve.
    private bool ResolveGameObject<T>(
        string typeName,
        string query,
        Func<IEnumerable<T>> candidates,
        Func<T, TextObject> nameOf,
        out T value,
        out string error,
        Func<T, Settlement> settlementOf = null) where T : class
    {
        Func<T, IEnumerable<string>> idsOf = IdsOf<T>;
        if (settlementOf != null)
            idsOf = candidate => IdsOfWithSettlement(candidate, settlementOf(candidate));

        bool hasCampaign = Campaign.Current != null;
        Func<IEnumerable<T>> liveCandidates = hasCampaign ? candidates : Enumerable.Empty<T>;
        if (TryMatch(typeName, query, liveCandidates, LookupRegistry<T>, false, idsOf, candidate => nameOf(candidate)?.ToString(), DisplayIdOf<T>, out value, out error))
            return true;

        if (!hasCampaign && !string.IsNullOrWhiteSpace(query))
            error = $"No {typeName} found with id '{query}'. {NoCampaignError} Names can only be matched in a campaign.";

        return false;
    }

    // Looks the query up as a registry id (bare or with the "{Type}_" prefix); untyped so an id of another type misses without logging a cast error.
    private T LookupRegistry<T>(string id) where T : class
    {
        if (objectManager.TryGetObject<object>($"{typeof(T).Name}_{id}", out var prefixed) && prefixed is T typed)
            return typed;

        return objectManager.TryGetObject<object>(id, out var found) ? found as T : null;
    }

    // The ids a candidate answers to: its registry id and, for MBObjectBase types, its StringId.
    private IEnumerable<string> IdsOf<T>(T candidate)
    {
        if (objectManager.TryGetId(candidate, out var registryId))
            yield return registryId;

        if (candidate is MBObjectBase mbObject)
            yield return mbObject.StringId;
    }

    // Component ids plus the owning Settlement's registry id and StringId, so town_ES1 resolves a Town.
    private IEnumerable<string> IdsOfWithSettlement<T>(T candidate, Settlement settlement)
    {
        foreach (var id in IdsOf(candidate))
            yield return id;

        if (settlement == null)
            yield break;

        foreach (var id in IdsOf(settlement))
            yield return id;
    }

    // The id printed in candidate lists: the registry id, falling back to StringId.
    private string DisplayIdOf<T>(T candidate) =>
        IdsOf(candidate).FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? "no id";

    // Pure resolution core: registry hit, then candidate id match, then unique case-insensitive name match.
    internal static bool TryMatch<T>(
        string typeName,
        string query,
        Func<IEnumerable<T>> candidates,
        Func<string, T> registryLookup,
        bool registryHitMustBeCandidate,
        Func<T, IEnumerable<string>> idsOf,
        Func<T, string> nameOf,
        Func<T, string> displayIdOf,
        out T value,
        out string error) where T : class
    {
        value = null;
        error = null;

        if (string.IsNullOrWhiteSpace(query))
        {
            error = EmptyQueryError(typeName);
            return false;
        }

        var trimmedQuery = query.Trim();

        // Ids resolve before the candidate list is built, so an id lookup never enumerates or names live objects.
        var registryHit = registryLookup(trimmedQuery);
        if (registryHit != null && !registryHitMustBeCandidate)
        {
            value = registryHit;
            return true;
        }

        var candidateList = candidates().Where(candidate => candidate != null).Distinct().ToList();
        if (registryHit != null && candidateList.Contains(registryHit))
        {
            value = registryHit;
            return true;
        }

        var idMatch = candidateList.FirstOrDefault(candidate => idsOf(candidate).Any(id => string.Equals(id, trimmedQuery, StringComparison.Ordinal)));
        if (idMatch != null)
        {
            value = idMatch;
            return true;
        }

        Func<T, string> safeNameOf = candidate => SafeName(nameOf, candidate);
        var nameMatches = candidateList.Where(candidate => NameEquals(safeNameOf(candidate), trimmedQuery)).ToList();
        if (nameMatches.Count == 1)
        {
            value = nameMatches[0];
            return true;
        }

        error = nameMatches.Count > 1
            ? AmbiguousError(typeName, trimmedQuery, nameMatches, safeNameOf, displayIdOf)
            : $"No {typeName} found with id or name '{query}'.";
        return false;
    }

    // Reads a candidate's name, skipping (and logging) one whose getter throws so one broken object can't fail every name lookup.
    private static string SafeName<T>(Func<T, string> nameOf, T candidate)
    {
        try
        {
            return nameOf(candidate);
        }
        catch (Exception ex)
        {
            // Lord parties whose leader hero is only partly built (e.g. E2E harness objects) throw from Name via Hero.EncyclopediaLink.
            Logger.Debug(ex, "Skipped {Type} candidate with an unreadable name during command name lookup", typeof(T).Name);
            return null;
        }
    }

    // Case-insensitive equality on trimmed names, skipping null or empty names.
    private static bool NameEquals(string name, string trimmedQuery)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
            return false;

        return string.Equals(trimmedName, trimmedQuery, StringComparison.OrdinalIgnoreCase);
    }

    // Builds the several-matches error listing up to 25 candidates as "Name (id)" ordered by id.
    private static string AmbiguousError<T>(string typeName, string query, List<T> matches, Func<T, string> nameOf, Func<T, string> displayIdOf)
    {
        var ordered = matches
            .Select(candidate => (Id: displayIdOf(candidate), Name: nameOf(candidate)?.Trim()))
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        var listed = string.Join(", ", ordered.Take(MaxListedCandidates).Select(entry => $"{entry.Name} ({entry.Id})"));
        var overflow = ordered.Count - MaxListedCandidates;
        if (overflow > 0)
            listed += $", ... and {overflow} more";

        return $"Several {Pluralize(typeName)} are named '{query}'; use an id. Candidates: {listed}";
    }

    // Plural of the friendly type name, e.g. Hero to Heroes and Mobile party to Mobile parties.
    private static string Pluralize(string typeName)
    {
        if (typeName.EndsWith("y", StringComparison.Ordinal))
            return typeName.Substring(0, typeName.Length - 1) + "ies";

        if (typeName.EndsWith("o", StringComparison.Ordinal) || typeName.EndsWith("s", StringComparison.Ordinal) || typeName.EndsWith("x", StringComparison.Ordinal))
            return typeName + "es";

        return typeName + "s";
    }

    // The error for a blank command argument.
    private static string EmptyQueryError(string typeName) => $"{typeName} name or id cannot be empty.";
}
