using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Locations.Commands;

/// <summary>
/// Commands for <see cref="Location"/>
/// </summary>
public class LocationDebugCommand
{
    // coop.debug.location.list
    /// <summary>
    /// Lists all registered locations
    /// </summary>
    [CommandLineArgumentFunction("list", "coop.debug.location")]
    public static string ListLocations(List<string> args)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to resolve {nameof(IObjectManager)}";
        }

        var stringBuilder = new StringBuilder();

        foreach (var settlement in Campaign.Current.Settlements)
        {
            if (settlement.LocationComplex == null) continue;

            foreach (var location in settlement.LocationComplex.GetListOfLocations())
            {
                if (objectManager.TryGetId(location, out var locationId) == false) continue;

                stringBuilder.AppendLine($"Id: '{locationId}' Characters: {location.GetCharacterList()?.Count() ?? 0} SpecialItems: {location.SpecialItems?.Count ?? 0}");
            }
        }

        return stringBuilder.ToString();
    }

    // coop.debug.location.info Location_town_V1_tavern
    /// <summary>
    /// Shows the state of a single location
    /// </summary>
    [CommandLineArgumentFunction("info", "coop.debug.location")]
    public static string Info(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.location.info <LocationId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"StringId: '{location.StringId}'");
        stringBuilder.AppendLine($"Characters: {location.GetCharacterList()?.Count() ?? 0}");
        stringBuilder.AppendLine($"SpecialItems: {location.SpecialItems?.Count ?? 0}");

        return stringBuilder.ToString();
    }

    // coop.debug.location.list_characters Location_town_V1_tavern
    /// <summary>
    /// Lists the characters currently in a location
    /// </summary>
    [CommandLineArgumentFunction("list_characters", "coop.debug.location")]
    public static string ListCharacters(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.location.list_characters <LocationId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;

        var stringBuilder = new StringBuilder();

        foreach (var locationCharacter in location.GetCharacterList() ?? Enumerable.Empty<LocationCharacter>())
        {
            stringBuilder.AppendLine($"'{locationCharacter.Character?.StringId}' Hero: {locationCharacter.Character?.IsHero} Tag: '{locationCharacter.SpecialTargetTag}'");
        }

        return stringBuilder.Length == 0 ? "No characters" : stringBuilder.ToString();
    }

    // coop.debug.location.list_special_items Location_town_V1_tavern
    /// <summary>
    /// Lists a location's special items
    /// </summary>
    [CommandLineArgumentFunction("list_special_items", "coop.debug.location")]
    public static string ListSpecialItems(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.location.list_special_items <LocationId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;

        var stringBuilder = new StringBuilder();

        foreach (var item in location.SpecialItems ?? Enumerable.Empty<ItemObject>().ToList())
        {
            stringBuilder.AppendLine($"'{item?.StringId}'");
        }

        return stringBuilder.Length == 0 ? "No special items" : stringBuilder.ToString();
    }

    // coop.debug.location.add_character Location_town_V1_tavern lord_1_1
    /// <summary>
    /// Adds a character to a location on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("add_character", "coop.debug.location")]
    public static string AddCharacter(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.location.add_character <LocationId> <CharacterObjectId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;
        if (TryResolveObject<CharacterObject>(args[1], out var character, out error) == false) return error;

        var locationCharacter = LocationCharacterFactory.Create(
            character,
            originParty: null,
            specialItem: null,
            spawnTag: "sp_notable",
            actionSetCode: null,
            behaviorsMethodName: null,
            characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
            fixedLocation: false,
            useCivilianEquipment: true);

        // The real mutator runs so the patched chokepoint broadcasts the change.
        GameLoopRunner.RunOnMainThread(() => location.AddCharacter(locationCharacter));

        return $"Added '{args[1]}' to '{args[0]}'";
    }

    // coop.debug.location.remove_character Location_town_V1_tavern lord_1_1
    /// <summary>
    /// Removes a character from a location on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("remove_character", "coop.debug.location")]
    public static string RemoveCharacter(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.location.remove_character <LocationId> <CharacterObjectId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;
        if (TryResolveObject<CharacterObject>(args[1], out var character, out error) == false) return error;

        var entry = SyncedLocationCharacters.Find(location, character);
        if (entry == null)
        {
            return $"No character '{args[1]}' in '{args[0]}'";
        }

        GameLoopRunner.RunOnMainThread(() => location.RemoveLocationCharacter(entry));

        return $"Removed '{args[1]}' from '{args[0]}'";
    }

    // coop.debug.location.remove_all_characters Location_town_V1_tavern
    /// <summary>
    /// Clears a location's character list on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("remove_all_characters", "coop.debug.location")]
    public static string RemoveAllCharacters(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.location.remove_all_characters <LocationId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;

        GameLoopRunner.RunOnMainThread(() => location.RemoveAllCharacters());

        return $"Cleared '{args[0]}'";
    }

    // coop.debug.location.add_special_item Location_town_V1_tavern mule
    /// <summary>
    /// Adds a special item to a location on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("add_special_item", "coop.debug.location")]
    public static string AddSpecialItem(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.location.add_special_item <LocationId> <ItemObjectId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;
        if (TryResolveObject<ItemObject>(args[1], out var item, out error) == false) return error;

        GameLoopRunner.RunOnMainThread(() => location.AddSpecialItem(item));

        return $"Added '{args[1]}' to '{args[0]}'";
    }

    // coop.debug.location.remove_special_item Location_town_V1_tavern mule
    /// <summary>
    /// Removes a special item from a location on the server and clients. Vanilla only removes
    /// special items from inside a mission scene, so the command publishes the removal directly.
    /// </summary>
    [CommandLineArgumentFunction("remove_special_item", "coop.debug.location")]
    public static string RemoveSpecialItem(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.location.remove_special_item <LocationId> <ItemObjectId>";
        }

        if (TryResolveLocation(args[0], out var location, out var error) == false) return error;
        if (TryResolveObject<ItemObject>(args[1], out var item, out error) == false) return error;

        if (location.SpecialItems?.Contains(item) != true)
        {
            return $"No item '{args[1]}' in '{args[0]}'";
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            location.SpecialItems.Remove(item);
            MessageBroker.Instance.Publish(location, new LocationSpecialItemRemoved(location, item));
        });

        return $"Removed '{args[1]}' from '{args[0]}'";
    }

    // coop.debug.location.populate town_V1
    /// <summary>
    /// Populates a settlement's locations and broadcasts the roster snapshot, as if a player
    /// party had entered
    /// </summary>
    [CommandLineArgumentFunction("populate", "coop.debug.location")]
    public static string Populate(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.location.populate <SettlementStringId>";
        }

        if (ContainerProvider.TryResolve<SettlementPopulationTracker>(out var tracker) == false)
        {
            return $"Unable to resolve {nameof(SettlementPopulationTracker)}";
        }

        var settlement = Campaign.Current.Settlements.FirstOrDefault(x => x.StringId == args[0]);
        if (settlement == null)
        {
            return $"Unable to find settlement '{args[0]}'";
        }

        tracker.DebugPopulate(settlement);

        return $"Populating '{args[0]}'";
    }

    private static bool TryResolveLocation(string locationId, out Location location, out string error)
    {
        location = null;
        error = null;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            error = $"Unable to resolve {nameof(IObjectManager)}";
            return false;
        }

        if (objectManager.TryGetObject(locationId, out location) == false)
        {
            error = $"Unable to find location '{locationId}'";
            return false;
        }

        return true;
    }

    private static bool TryResolveObject<T>(string id, out T obj, out string error) where T : class
    {
        obj = null;
        error = null;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            error = $"Unable to resolve {nameof(IObjectManager)}";
            return false;
        }

        if (objectManager.TryGetObject(id, out obj) == false)
        {
            error = $"Unable to find object '{id}'";
            return false;
        }

        return true;
    }
}
