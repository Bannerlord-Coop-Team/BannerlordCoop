using Autofac;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class MapEventDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventDebugCommands>();

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

    // coop.debug.mapevent.start_looter
    /// <summary>
    /// Starts combat with looter
    /// </summary>
    [CommandLineArgumentFunction("start_looter", "coop.debug.mapevent")]
    public static string StartRandomLooterMapEvent(List<string> args)
    {
        //if (args.Count != 2)
        //{
        //    return "Usage: coop.debug.besiegercamp.set_number_of_troops_killed_on_side <besiegerCampId> <value> ";
        //}

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject("sea_raiders_1", out PartyBase partyBase))
        {
            return $"BesiegerCamp with ID: sea_raiders_1 not found";
        }

        EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, partyBase);


        return $"MapEvent Started";
    }

    // coop.debug.mapevent.start_nearest_looter
    /// <summary>
    /// Forces an encounter between the player's party and the nearest active bandit/looter party, so
    /// the bandit surrender/recruit dialogue can be reached without chasing one down. Run on a client
    /// (uses the player's main party). Bring a much larger party than the bandits so they offer to
    /// surrender or join.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_looter", "coop.debug.mapevent")]
    public static string StartNearestLooterMapEvent(List<string> args)
    {
        if (!TryGetObjectManager(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
        {
            return "No main party — run this on a client with a player party.";
        }

        var mainPos = mainParty.Position.ToVec2();
        var nearest = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != mainParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(mainPos))
            .FirstOrDefault();

        if (nearest == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        EncounterManager.StartPartyEncounter(mainParty.Party, nearest.Party);

        var partyId = objectManager.TryGetId(nearest, out string registryId) ? registryId : nearest.StringId;

        return $"Started encounter with {nearest.Name} (StringId {nearest.StringId}, registry id {partyId}), " +
               $"{nearest.MemberRoster.TotalManCount} troops, {nearest.Position.ToVec2().Distance(mainPos):0.0} away.";
    }

    /// <summary>
    /// Kills a random troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_random_troop", "coop.debug.mapevent")]
    public static string KillRandomTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        var party = enemySide.Parties[MBRandom.RandomInt(enemySide.Parties.Count)];
        if (party is null)
        {
            return "Enemy side has no parties";
        }

        var troops = party.Troops;
        if (troops is null || troops.Count() == 0)
        {
            return "Enemy party has no troops";
        }

        var entries = troops._elementDictionary.ToArray();

        if (entries.Length == 0)
        {
            return "Enemy party has no troops";
        }

        var randomEntry = entries[MBRandom.RandomInt(entries.Length)];

        UniqueTroopDescriptor descriptor = randomEntry.Key;
        FlattenedTroopRosterElement troopElement = randomEntry.Value;

        try
        {
            enemySide.OnTroopKilled(descriptor);
        }
        catch (Exception ex)
        {
            return $"Failed to kill random troop: {ex.Message}";
        }

        return $"Killed random troop: {troopElement.Troop?.Name}";
    }

    /// <summary>
    /// Kills all but one troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_all_but_one", "coop.debug.mapevent")]
    public static string KillAllButOneTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        if (enemySide.Parties is null || enemySide.Parties.Count == 0)
        {
            return "Enemy side has no parties";
        }

        var allTroops = new List<(MapEventParty Party, UniqueTroopDescriptor Descriptor, FlattenedTroopRosterElement Element)>();

        foreach (var party in enemySide.Parties)
        {
            if (party?.Troops?._elementDictionary is null)
                continue;

            foreach (var entry in party.Troops._elementDictionary)
            {
                var descriptor = entry.Key;
                var element = entry.Value;

                allTroops.Add((party, descriptor, element));
            }
        }

        if (allTroops.Count == 0)
        {
            return "Enemy side has no troops";
        }

        if (allTroops.Count == 1)
        {
            return $"Enemy side already has only one troop: {allTroops[0].Element.Troop?.Name}";
        }

        var survivorIndex = MBRandom.RandomInt(allTroops.Count);
        var survivor = allTroops[survivorIndex];

        var killedCount = 0;

        for (var i = 0; i < allTroops.Count; i++)
        {
            if (i == survivorIndex)
                continue;

            try
            {
                enemySide.OnTroopKilled(allTroops[i].Descriptor);
                killedCount++;
            }
            catch (Exception ex)
            {

            }
        }

        return $"Killed {killedCount} troops. Survivor: {survivor.Element.Troop?.Name}";
    }

    /// <summary>
    /// Lists the fields and properties of the current PlayerEncounter.
    /// </summary>
    [CommandLineArgumentFunction("list_player_encounter", "coop.debug.mapevent")]
    public static string ListPlayerEncounter(List<string> args)
    {
        var playerEncounter = PlayerEncounter.Current;
        if (playerEncounter == null)
        {
            return "No current PlayerEncounter";
        }

        var sb = new StringBuilder();

        sb.AppendLine("PlayerEncounter:");
        AppendObjectDetails(sb, playerEncounter, "\t", "PlayerEncounter Details");

        var result = sb.ToString();

        Logger.Debug("{PlayerEncounter}", result);

        return result;
    }

    /// <summary>
    /// Prints a compact, teardown-focused snapshot of the current <see cref="PlayerEncounter"/> and the main
    /// party's map-event state. Run on each client after a battle to spot an encounter that did not tear down —
    /// e.g. PlayerEncounter.Current still PRESENT, or MainParty.MapEvent lingering on an already-finalized event.
    /// Unlike <c>list_player_encounter</c> (full reflection dump) this is short enough to diff across clients.
    /// </summary>
    [CommandLineArgumentFunction("encounter_state", "coop.debug.mapevent")]
    public static string EncounterState(List<string> args)
    {
        TryGetObjectManager(out var objectManager);

        var sb = new StringBuilder();

        var encounter = PlayerEncounter.Current;
        sb.AppendLine($"PlayerEncounter.Current: {(encounter == null ? "<null> (torn down)" : "PRESENT")}");
        if (encounter != null)
        {
            sb.AppendLine($"\tBattle:           {FormatMapEvent(PlayerEncounter.Battle, objectManager)}");
            sb.AppendLine($"\t_mapEvent:        {FormatMapEvent(encounter._mapEvent, objectManager)}");
            sb.AppendLine($"\tEncounteredParty: {FormatPartyBase(PlayerEncounter.EncounteredParty)}");
            sb.AppendLine($"\t_attackerParty:   {FormatPartyBase(encounter._attackerParty)}");
            sb.AppendLine($"\t_defenderParty:   {FormatPartyBase(encounter._defenderParty)}");
        }

        var mainParty = MobileParty.MainParty;
        sb.AppendLine($"MainParty.MapEvent:      {FormatMapEvent(mainParty?.MapEvent, objectManager)}");

        var side = mainParty?.Party?.MapEventSide;
        if (side == null)
            sb.AppendLine("MainParty.MapEventSide:  <null>");
        else
            sb.AppendLine($"MainParty.MapEventSide:  leader={FormatPartyBase(side.LeaderParty)} mainPartyIsLeader={side.LeaderParty == mainParty?.Party}");

        sb.AppendLine($"CurrentMenu:             {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}");
        sb.AppendLine($"MissionState.Current:    {(MissionState.Current == null ? "<null>" : "PRESENT")}");

        var result = sb.ToString();
        Logger.Debug("{EncounterState}", result);
        return result;
    }

    private static string FormatMapEvent(MapEvent mapEvent, IObjectManager objectManager)
    {
        if (mapEvent == null) return "<null>";

        var id = "<no id>";
        if (objectManager != null && objectManager.TryGetId(mapEvent, out var resolved))
            id = resolved;

        return $"id={id} finalized={mapEvent.IsFinalized} state={mapEvent.BattleState} winner={mapEvent.WinningSide}";
    }

    [CommandLineArgumentFunction("get_events", "coop.debug.mapevent")]
    public static string GetEvents(List<string> args)
    {
        var sb = new StringBuilder();

        if(!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        foreach(var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (objectManager.TryGetIdWithLogging(mapEvent, out var id))
            {
                sb.AppendLine($"Map event id: {id}");
            }

            var partyNames = mapEvent.AttackerSide.Parties?
                .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
                .ToArray() ?? Array.Empty<string>();
            sb.AppendLine($"\tAttacker: {string.Join(",", FormatSideNames(mapEvent.AttackerSide))}");
            sb.AppendLine($"\tDefender: {string.Join(",", FormatSideNames(mapEvent.DefenderSide))}");
        }

        return sb.ToString();
    }

    private static string[] FormatSideNames(MapEventSide side)
    {
        if (side == null)
            return new string[] { "<null>" };

        return side.Parties?
            .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
            .ToArray() ?? Array.Empty<string>();
    }

    [CommandLineArgumentFunction("get_event", "coop.debug.mapevent")]
    public static string GetEvent(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.get_event <mapEventId>";
        }

        if (!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        var mapEventId = args[0];

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
        {
            return $"Failed to find MapEvent with id: {mapEventId}";
        }

        var sb = new StringBuilder();

        sb.AppendLine($"Map event id: {mapEventId}");
        sb.AppendLine();

        AppendMapEventSummary(sb, mapEvent);
        sb.AppendLine();

        var result = sb.ToString();

        Logger.Debug("{MapEvent}", result);

        return result;
    }

    private static void AppendMapEventSummary(StringBuilder sb, MapEvent mapEvent)
    {
        sb.AppendLine("Summary:");

        AppendSideSummary(sb, "Attacker", mapEvent.AttackerSide);
        AppendSideSummary(sb, "Defender", mapEvent.DefenderSide);
    }

    private static void AppendSideSummary(StringBuilder sb, string sideName, MapEventSide side)
    {
        if (side == null)
        {
            sb.AppendLine($"\t{sideName}: <null>");
            return;
        }

        sb.AppendLine($"\t{sideName}: {string.Join(", ", FormatSideNames(side))}");

        AppendObjectDetails(sb, side, "\t\t", "Side Details");

        sb.AppendLine("\t\tParties:");

        var parties = side.Parties;
        if (parties == null)
        {
            sb.AppendLine("\t\t\t<null>");
            return;
        }

        var index = 0;
        foreach (var party in parties)
        {
            sb.AppendLine($"\t\t\tParty[{index}]:");

            if (party == null)
            {
                sb.AppendLine("\t\t\t\t<null>");
            }
            else
            {
                AppendMapEventPartyDetails(sb, party, "\t\t\t\t");
            }

            index++;
        }
    }
    private static void AppendMapEventPartyDetails(StringBuilder sb, MapEventParty party, string indent)
    {
        var partyName = party.Party?.Name?.ToString() ?? "<null>";
        sb.AppendLine($"{indent}Party: {partyName}");

        AppendObjectDetails(sb, party, indent, "MapEventParty Details");
    }

    private static void AppendObjectDetails(StringBuilder sb, object obj, string indent, string title)
    {
        if (obj == null)
        {
            sb.AppendLine($"{indent}{title}: <null>");
            return;
        }

        var type = obj.GetType();

        sb.AppendLine($"{indent}{title}: {GetFriendlyTypeName(type)}");

        AppendFields(sb, obj, type, indent + "\t");
        AppendProperties(sb, obj, type, indent + "\t");
    }

    private static void AppendFields(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Fields:");

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (fields.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var field in fields.OrderBy(f => f.Name))
        {
            object value;

            try
            {
                value = field.GetValue(obj);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{field.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{field.Name}: {FormatValue(value)}");
        }
    }

    private static void AppendProperties(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Properties:");

        var properties = type.GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (properties.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var property in properties.OrderBy(p => p.Name))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <indexed property>");
                continue;
            }

            object value;

            try
            {
                value = property.GetValue(obj, null);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{property.Name}: {FormatValue(value)}");
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "<null>";

        if (value is string str)
            return str;

        if (value is TextObject textObject)
            return textObject.ToString();

        if (value is CharacterObject character)
            return FormatCharacter(character);

        if (value is MobileParty mobileParty)
            return FormatMobileParty(mobileParty);

        if (value is PartyBase partyBase)
            return FormatPartyBase(partyBase);

        if (value is IFaction faction)
            return faction.Name?.ToString() ?? faction.StringId ?? "<unnamed faction>";

        if (value is UniqueTroopDescriptor descriptor)
            return descriptor.ToString();

        if (value is IEnumerable enumerable && !(value is string))
            return FormatEnumerable(enumerable);

        return value.ToString();
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var values = new List<string>();
        var count = 0;

        foreach (var item in enumerable)
        {
            if (count >= 20)
            {
                values.Add("...");
                break;
            }

            values.Add(FormatValue(item));
            count++;
        }

        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatCharacter(CharacterObject character)
    {
        if (character == null)
            return "<null>";

        var id = character.StringId ?? "<no id>";
        var name = character.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatMobileParty(MobileParty party)
    {
        if (party == null)
            return "<null>";

        var id = party.StringId ?? "<no id>";
        var name = party.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatPartyBase(PartyBase party)
    {
        if (party == null)
            return "<null>";

        var name = party.Name?.ToString() ?? "<no name>";

        return name;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "<null>";

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tickIndex = genericTypeName.IndexOf('`');

        if (tickIndex >= 0)
            genericTypeName = genericTypeName.Substring(0, tickIndex);

        var genericArguments = type.GetGenericArguments()
            .Select(GetFriendlyTypeName)
            .ToArray();

        return genericTypeName + "<" + string.Join(", ", genericArguments) + ">";
    }
}
