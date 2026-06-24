using Autofac;
using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Party.Commands;

internal class PartyCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyCommands>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// All alive heroes whose full name matches and that have a party (the console splits arguments on spaces,
    /// so the caller joins them back). The bulk cheats act on every match, not just the first, so a co-op test
    /// save with several identically-named heroes - e.g. multiple "RandomPlayer" parties - is set up on all of
    /// them at once. Heroes with no party (a prisoner, notable, or wanderer) are skipped so a cheat never
    /// dereferences a null PartyBelongedTo.
    /// </summary>
    private static List<Hero> FindHeroesWithParty(string name)
        => Hero.AllAliveHeroes.Where(h => h.Name.ToString() == name && h.PartyBelongedTo != null).ToList();

    /// <summary>
    /// Finds a single alive hero by full name with a party, for the cheats that target one party (the
    /// companion-preserve pair - putting one hero into several prisons at once would be invalid state).
    /// Reports cleanly when the name matches nothing or only partyless heroes.
    /// </summary>
    private static bool TryGetHeroWithParty(string name, out Hero hero, out string error)
    {
        hero = FindHeroesWithParty(name).FirstOrDefault();
        if (hero == null)
        {
            error = Hero.AllAliveHeroes.Any(h => h.Name.ToString() == name)
                ? "Hero \"" + name + "\" has no party (a prisoner, notable, or wanderer?)."
                : "Hero \"" + name + "\" not found.";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// View character ids in a hero's party
    /// </summary>
    [CommandLineArgumentFunction("characterids", "coop.debug.mobileparty")]
    public static string ViewItemIdsCommand(List<string> strings)
    {
        if (strings.Count == 0) return "Hero name argument required.";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            stringBuilder.AppendLine("##" + hero.Name.ToString());
            stringBuilder.AppendLine("Member roster:");
            foreach (var rosterElement in hero.PartyBelongedTo.MemberRoster.data)
            {
                stringBuilder.AppendLine(rosterElement.Character?.StringId + ": " + rosterElement.Number + " " + rosterElement.Xp);
            }

            stringBuilder.AppendLine("Prisoner roster:");
            foreach (var rosterElement in hero.PartyBelongedTo.PrisonRoster.data)
            {
                stringBuilder.AppendLine(rosterElement.Character?.StringId + ": " + rosterElement.Number + " " + rosterElement.Xp);
            }
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Add xp to all troops in a hero's party
    /// </summary>
    [CommandLineArgumentFunction("addtroopxp", "coop.debug.mobileparty")]
    public static string AddTroopXpCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count < 2) return "Hero name and xp amount required.";

        // The xp amount is the last token; the rest is the (possibly multi-word) hero name.
        if (!int.TryParse(strings[strings.Count - 1], out int xpGain)) return "Please enter an integer for xp amount";

        var name = string.Join(" ", strings.Take(strings.Count - 1));
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        foreach (var hero in heroes)
        {
            var memberRoster = hero.PartyBelongedTo.MemberRoster;
            foreach (var troop in memberRoster.data)
            {
                memberRoster.AddXpToTroop(troop.Character, xpGain);
            }
        }

        return "Gave xp to the troops of " + heroes.Count + " party/parties named \"" + name + "\".";
    }

    /// <summary>
    /// Add troops to a hero's party
    /// </summary>
    [CommandLineArgumentFunction("addtroops", "coop.debug.mobileparty")]
    public static string AddRecruitsCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        var troopsToAdd = new Dictionary<string, int>()
        {
            { "imperial_vigla_recruit", 5 },
            { "imperial_recruit", 2 },
            { "imperial_equite", 2 },
            { "imperial_heavy_horseman", 2 }
        };

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var memberRoster = hero.PartyBelongedTo.MemberRoster;
            foreach (var troopId in troopsToAdd.Keys)
            {
                if (!objectManager.TryGetObject(troopId, out CharacterObject characterObject))
                {
                    stringBuilder.AppendLine("Failed to retrieve object for CharacterObject id: " + troopId);
                }
                else
                {
                    memberRoster.AddToCounts(characterObject, troopsToAdd[troopId]);
                }
            }

            stringBuilder.AppendLine(hero.Name.ToString() + " was given troops.");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Add prisoners to a hero's party
    /// </summary>
    [CommandLineArgumentFunction("addprisoners", "coop.debug.mobileparty")]
    public static string AddPrisonersCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        var troopsToAdd = new Dictionary<string, int>()
        {
            { "imperial_vigla_recruit", 5 },
            { "imperial_recruit", 2 },
            { "imperial_equite", 2 },
            { "imperial_heavy_horseman", 2 }
        };

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var prisonerRoster = hero.PartyBelongedTo.PrisonRoster;
            foreach (var troopId in troopsToAdd.Keys)
            {
                if (!objectManager.TryGetObject(troopId, out CharacterObject characterObject))
                {
                    stringBuilder.AppendLine("Failed to retrieve object for CharacterObject id: " + troopId);
                }
                else
                {
                    prisonerRoster.AddToCounts(characterObject, troopsToAdd[troopId]);
                }
            }

            stringBuilder.AppendLine(hero.Name.ToString() + " was given prisoners.");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Remove all prisoners from a hero's party
    /// </summary>
    [CommandLineArgumentFunction("removeprisoners", "coop.debug.mobileparty")]
    public static string RemovePrisonersCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var prisonerRoster = hero.PartyBelongedTo.PrisonRoster;

            // Walk from the end so removing the current element leaves the lower indices valid. Each
            // subtract-to-zero with removeDepleted runs with patches live, so it replicates to clients.
            for (int i = prisonerRoster.Count - 1; i >= 0; i--)
            {
                var element = prisonerRoster.GetElementCopyAtIndex(i);
                prisonerRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber, 0, true);
            }

            stringBuilder.AppendLine(hero.Name.ToString() + " had their prisoners removed.");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Put a hero (e.g. a player companion) into a hero's party prison roster, to set up the
    /// companion-preserve test. Args: captor hero name, prisoner hero name.
    /// </summary>
    [CommandLineArgumentFunction("imprison_companion", "coop.debug.mobileparty")]
    public static string ImprisonCompanionCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count < 2) return "Captor hero name and prisoner hero name required.";

        // The console splits arguments on spaces, so the captor is the first token and the prisoner name is
        // the rest joined back together. Companions always have a multi-word name (e.g. "Chandion the Bull"),
        // which would otherwise arrive as several tokens and never match. (The captor must be a single-token
        // name for this split to work, which the player's own hero typically is.) One captor only: a hero can
        // only be a prisoner in one place, so imprisoning the companion in several prisons would be invalid.
        if (!TryGetHeroWithParty(strings[0], out var captor, out var error)) return error;

        var prisonerName = string.Join(" ", strings.Skip(1));
        var prisoner = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString() == prisonerName);
        if (prisoner == null) return "Prisoner hero \"" + prisonerName + "\" not found.";

        // The preserve only fires for a player companion, so a non-companion would be removed by both the
        // fixed and the old code and prove nothing. Require a companion so the test actually exercises it.
        if (!prisoner.IsPlayerCompanion) return prisoner.Name + " is not a player companion; this test needs one.";

        captor.PartyBelongedTo.PrisonRoster.AddToCounts(prisoner.CharacterObject, 1);
        return prisoner.Name + " (a player companion) added to " + captor.Name + "'s prison roster.";
    }

    /// <summary>
    /// Apply a whole-roster snapshot to a hero's party prison roster with the hero prisoners stripped out, as
    /// if the server freed them. Drives TroopRosterInterface.UpdateWithData on a prison roster: hero prisoners
    /// (player companions included) must be removed, not preserved, and the removal replicates to clients.
    /// </summary>
    [CommandLineArgumentFunction("snapshot_prison", "coop.debug.mobileparty")]
    public static string SnapshotPrisonCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        // One party only, to pair with imprison_companion (which targets one prison).
        if (!TryGetHeroWithParty(string.Join(" ", strings), out var hero, out var error)) return error;

        var prisonRoster = hero.PartyBelongedTo.PrisonRoster;
        if (ContainerProvider.TryGetContainer(out var container) == false ||
            container.TryResolve(out ITroopRosterInterface troopRosterInterface) == false)
            return "Unable to resolve TroopRosterInterface.";

        // Pack the prison roster, then drop the hero elements so the snapshot no longer carries them, as if
        // the server had freed them. Resolve each element's CharacterObject to tell heroes from basic troops.
        var packed = troopRosterInterface.PackTroopRosterData(prisonRoster);
        var nonHeroElements = new List<TroopRosterElementData>();
        foreach (var element in packed.Data)
        {
            if (objectManager.TryGetObject(element.CharacterId, out CharacterObject troop) && troop.IsHero) continue;
            nonHeroElements.Add(element);
        }
        var snapshot = new TroopRosterData(nonHeroElements);

        // Pass a non-null mainHero so the preserve decision turns on the prison-vs-member roster check (the
        // thing under test), not on a null mainHero short-circuit.
        troopRosterInterface.UpdateWithData(prisonRoster, snapshot, hero);

        int heroesLeft = 0;
        for (int i = 0; i < prisonRoster.Count; i++)
        {
            if (prisonRoster.GetElementCopyAtIndex(i).Character?.IsHero == true) heroesLeft++;
        }

        return heroesLeft == 0
            ? "Applied prison snapshot to " + hero.Name + "; all hero prisoners removed (companion-preserve correctly off for prison rosters)."
            : "Applied prison snapshot to " + hero.Name + "; " + heroesLeft + " hero prisoner(s) still present (companion-preserve wrongly kept them).";
    }
}
