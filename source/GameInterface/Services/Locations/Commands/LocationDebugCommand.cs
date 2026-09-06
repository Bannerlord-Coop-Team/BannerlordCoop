using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
namespace GameInterface.Services.Locations.Commands;

/// <summary>
/// Commands for <see cref="Location"/>
/// </summary>
public class LocationDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class EnterLocationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "enter";

        public string Description => "Enters the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (TryResolveLocation(args[0], out var location, out var error) == false)
                return Failed(error);

            var settlement = Campaign.Current.Settlements.FirstOrDefault(
                candidate => candidate.LocationComplex?
                    .GetListOfLocations()
                    .Contains(location) == true);
            if (settlement == null)
                return Failed($"Unable to resolve the settlement for location '{args[0]}'.");

            if (PlayerEncounter.Current == null)
            {
                if (!ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface))
                    return Failed("Unable to resolve the settlement interface.");

                settlementInterface.StartSettlementEncounter(MobileParty.MainParty, settlement);
                return PlayerEncounter.Current == null
                    ? Failed($"Unable to start a local encounter with '{settlement.StringId}'.")
                    : Succeeded($"Started a local encounter with '{settlement.StringId}'.");
            }

            if (PlayerEncounter.EncounterSettlement != settlement)
                return Failed($"A player encounter with another settlement is already active.");

            if (Mission.Current != null)
                return Failed("A mission is already active.");

            PlayerEncounter.EnterSettlement();
            var mission = PlayerEncounter.LocationEncounter?
                .CreateAndOpenMissionController(location);
            return mission == null
                ? Failed($"Unable to open location '{args[0]}'.")
                : Succeeded($"Opening location '{args[0]}'.");

        }
    }

    public sealed class LeaveLocationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "leave";

        public string Description => "Leaves the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (Mission.Current == null)
                return Failed("No mission is active.");

            Mission.Current.EndMission();
            return Succeeded("Ending the current location mission.");

        }
    }

    // coop.debug.location.list
    /// <summary>
    /// Lists all registered locations
    /// </summary>
    public sealed class ListLocationsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "list";

        public string Description => "Lists the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to resolve {nameof(IObjectManager)}");
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

            return Succeeded(stringBuilder.ToString());

        }
    }

    // coop.debug.location.info Location_town_V1_tavern
    /// <summary>
    /// Shows the state of a single location
    /// </summary>
    public sealed class InfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "info";

        public string Description => "Shows the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"StringId: '{location.StringId}'");
            stringBuilder.AppendLine($"Characters: {location.GetCharacterList()?.Count() ?? 0}");
            stringBuilder.AppendLine($"SpecialItems: {location.SpecialItems?.Count ?? 0}");

            return Succeeded(stringBuilder.ToString());

        }
    }

    // coop.debug.location.list_characters Location_town_V1_tavern
    /// <summary>
    /// Lists the characters currently in a location
    /// </summary>
    public sealed class ListCharactersCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "list_characters";

        public string Description => "Lists characters for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);

            var stringBuilder = new StringBuilder();

            foreach (var locationCharacter in location.GetCharacterList() ?? Enumerable.Empty<LocationCharacter>())
            {
                stringBuilder.AppendLine($"'{locationCharacter.Character?.StringId}' Hero: {locationCharacter.Character?.IsHero} Tag: '{locationCharacter.SpecialTargetTag}'");
            }

            return Succeeded(stringBuilder.Length == 0 ? "No characters" : stringBuilder.ToString());

        }
    }

    // coop.debug.location.list_special_items Location_town_V1_tavern
    /// <summary>
    /// Lists a location's special items
    /// </summary>
    public sealed class ListSpecialItemsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "list_special_items";

        public string Description => "Lists special items for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);

            var stringBuilder = new StringBuilder();

            foreach (var item in location.SpecialItems ?? Enumerable.Empty<ItemObject>().ToList())
            {
                stringBuilder.AppendLine($"'{item?.StringId}'");
            }

            return Succeeded(stringBuilder.Length == 0 ? "No special items" : stringBuilder.ToString());

        }
    }

    // coop.debug.location.add_character Location_town_V1_tavern lord_1_1
    /// <summary>
    /// Adds a character to a location on the server and clients
    /// </summary>
    public sealed class AddCharacterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "add_character";

        public string Description => "Adds character for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
            new ExpectedArgs("characterObjectId", "The character object id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }


            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);
            if (TryResolveObject<CharacterObject>(args[1], out var character, out error) == false) return Failed(error);

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
            GameThread.Run(() => location.AddCharacter(locationCharacter));

            return Succeeded($"Added '{args[1]}' to '{args[0]}'");

        }
    }

    // coop.debug.location.remove_character Location_town_V1_tavern lord_1_1
    /// <summary>
    /// Removes a character from a location on the server and clients
    /// </summary>
    public sealed class RemoveCharacterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "remove_character";

        public string Description => "Removes character for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
            new ExpectedArgs("characterObjectId", "The character object id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }


            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);
            if (TryResolveObject<CharacterObject>(args[1], out var character, out error) == false) return Failed(error);

            var entry = SyncedLocationCharacters.Find(location, character);
            if (entry == null)
            {
                return Failed($"No character '{args[1]}' in '{args[0]}'");
            }

            GameThread.Run(() => location.RemoveLocationCharacter(entry));

            return Succeeded($"Removed '{args[1]}' from '{args[0]}'");

        }
    }

    // coop.debug.location.remove_all_characters Location_town_V1_tavern
    /// <summary>
    /// Clears a location's character list on the server and clients
    /// </summary>
    public sealed class RemoveAllCharactersCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "remove_all_characters";

        public string Description => "Removes all characters for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }


            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);

            GameThread.Run(() => location.RemoveAllCharacters());

            return Succeeded($"Cleared '{args[0]}'");

        }
    }

    // coop.debug.location.add_special_item Location_town_V1_tavern mule
    /// <summary>
    /// Adds a special item to a location on the server and clients
    /// </summary>
    public sealed class AddSpecialItemCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "add_special_item";

        public string Description => "Adds special item for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
            new ExpectedArgs("itemObjectId", "The item object id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }


            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);
            if (TryResolveObject<ItemObject>(args[1], out var item, out error) == false) return Failed(error);

            GameThread.Run(() => location.AddSpecialItem(item));

            return Succeeded($"Added '{args[1]}' to '{args[0]}'");

        }
    }

    // coop.debug.location.remove_special_item Location_town_V1_tavern mule
    /// <summary>
    /// Removes a special item from a location on the server and clients. Vanilla only removes
    /// special items from inside a mission scene, so the command publishes the removal directly.
    /// </summary>
    public sealed class RemoveSpecialItemCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "remove_special_item";

        public string Description => "Removes special item for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("locationId", "The location id."),
            new ExpectedArgs("itemObjectId", "The item object id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }


            if (TryResolveLocation(args[0], out var location, out var error) == false) return Failed(error);
            if (TryResolveObject<ItemObject>(args[1], out var item, out error) == false) return Failed(error);

            if (location.SpecialItems?.Contains(item) != true)
            {
                return Failed($"No item '{args[1]}' in '{args[0]}'");
            }

            GameThread.Run(() =>
            {
                location.SpecialItems.Remove(item);
                MessageBroker.Instance.Publish(location, new LocationSpecialItemRemoved(location, item));
            });

            return Succeeded($"Removed '{args[1]}' from '{args[0]}'");

        }
    }

    // coop.debug.location.populate town_V1
    /// <summary>
    /// Populates a settlement's locations and broadcasts the roster snapshot, as if a player
    /// party had entered
    /// </summary>
    public sealed class PopulateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.location";

        public string Name => "populate";

        public string Description => "Runs the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementStringId", "The settlement string id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Command is only available to run on the server");
            }


            if (ContainerProvider.TryResolve<SettlementPopulationTracker>(out var tracker) == false)
            {
                return Failed($"Unable to resolve {nameof(SettlementPopulationTracker)}");
            }

            var settlement = Campaign.Current.Settlements.FirstOrDefault(x => x.StringId == args[0]);
            if (settlement == null)
            {
                return Failed($"Unable to find settlement '{args[0]}'");
            }

            tracker.DebugPopulate(settlement);

            return Succeeded($"Populating '{args[0]}'");

        }
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
