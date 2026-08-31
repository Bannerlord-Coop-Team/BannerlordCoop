using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Locations.Commands;

public interface IEnterLocationCoopCommand : ICoopCommand
{
}

public sealed class EnterLocationCoopCommand : LegacyCoopCommand, IEnterLocationCoopCommand
{
    public EnterLocationCoopCommand()
        : base(
            "coop.debug.location",
            "enter",
            "Enters the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
            },
            LocationDebugCommand.EnterLocation)
    {
    }
}

public interface ILeaveLocationCoopCommand : ICoopCommand
{
}

public sealed class LeaveLocationCoopCommand : LegacyCoopCommand, ILeaveLocationCoopCommand
{
    public LeaveLocationCoopCommand()
        : base(
            "coop.debug.location",
            "leave",
            "Leaves the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            LocationDebugCommand.LeaveLocation)
    {
    }
}

public interface IListLocationsCoopCommand : ICoopCommand
{
}

public sealed class ListLocationsCoopCommand : LegacyCoopCommand, IListLocationsCoopCommand
{
    public ListLocationsCoopCommand()
        : base(
            "coop.debug.location",
            "list",
            "Lists the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            LocationDebugCommand.ListLocations)
    {
    }
}

public interface IInfoCoopCommand : ICoopCommand
{
}

public sealed class InfoCoopCommand : LegacyCoopCommand, IInfoCoopCommand
{
    public InfoCoopCommand()
        : base(
            "coop.debug.location",
            "info",
            "Shows the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
            },
            LocationDebugCommand.Info)
    {
    }
}

public interface IListCharactersCoopCommand : ICoopCommand
{
}

public sealed class ListCharactersCoopCommand : LegacyCoopCommand, IListCharactersCoopCommand
{
    public ListCharactersCoopCommand()
        : base(
            "coop.debug.location",
            "list_characters",
            "Lists characters for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
            },
            LocationDebugCommand.ListCharacters)
    {
    }
}

public interface IListSpecialItemsCoopCommand : ICoopCommand
{
}

public sealed class ListSpecialItemsCoopCommand : LegacyCoopCommand, IListSpecialItemsCoopCommand
{
    public ListSpecialItemsCoopCommand()
        : base(
            "coop.debug.location",
            "list_special_items",
            "Lists special items for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
            },
            LocationDebugCommand.ListSpecialItems)
    {
    }
}

public interface IAddCharacterCoopCommand : ICoopCommand
{
}

public sealed class AddCharacterCoopCommand : LegacyCoopCommand, IAddCharacterCoopCommand
{
    public AddCharacterCoopCommand()
        : base(
            "coop.debug.location",
            "add_character",
            "Adds character for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
                new ExpectedArgs("characterObjectId", "The character object id."),
            },
            LocationDebugCommand.AddCharacter)
    {
    }
}

public interface IRemoveCharacterCoopCommand : ICoopCommand
{
}

public sealed class RemoveCharacterCoopCommand : LegacyCoopCommand, IRemoveCharacterCoopCommand
{
    public RemoveCharacterCoopCommand()
        : base(
            "coop.debug.location",
            "remove_character",
            "Removes character for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
                new ExpectedArgs("characterObjectId", "The character object id."),
            },
            LocationDebugCommand.RemoveCharacter)
    {
    }
}

public interface IRemoveAllCharactersCoopCommand : ICoopCommand
{
}

public sealed class RemoveAllCharactersCoopCommand : LegacyCoopCommand, IRemoveAllCharactersCoopCommand
{
    public RemoveAllCharactersCoopCommand()
        : base(
            "coop.debug.location",
            "remove_all_characters",
            "Removes all characters for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
            },
            LocationDebugCommand.RemoveAllCharacters)
    {
    }
}

public interface IAddSpecialItemCoopCommand : ICoopCommand
{
}

public sealed class AddSpecialItemCoopCommand : LegacyCoopCommand, IAddSpecialItemCoopCommand
{
    public AddSpecialItemCoopCommand()
        : base(
            "coop.debug.location",
            "add_special_item",
            "Adds special item for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
                new ExpectedArgs("itemObjectId", "The item object id."),
            },
            LocationDebugCommand.AddSpecialItem)
    {
    }
}

public interface IRemoveSpecialItemCoopCommand : ICoopCommand
{
}

public sealed class RemoveSpecialItemCoopCommand : LegacyCoopCommand, IRemoveSpecialItemCoopCommand
{
    public RemoveSpecialItemCoopCommand()
        : base(
            "coop.debug.location",
            "remove_special_item",
            "Removes special item for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("locationId", "The location id."),
                new ExpectedArgs("itemObjectId", "The item object id."),
            },
            LocationDebugCommand.RemoveSpecialItem)
    {
    }
}

public interface IPopulateCoopCommand : ICoopCommand
{
}

public sealed class PopulateCoopCommand : LegacyCoopCommand, IPopulateCoopCommand
{
    public PopulateCoopCommand()
        : base(
            "coop.debug.location",
            "populate",
            "Runs the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementStringId", "The settlement string id."),
            },
            LocationDebugCommand.Populate)
    {
    }
}
