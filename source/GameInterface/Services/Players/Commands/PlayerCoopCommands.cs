using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Players.Commands;

public interface IPlayerListCommand : ICoopCommand
{
}

public sealed class PlayerListCommand : LegacyCoopCommand, IPlayerListCommand
{
    public PlayerListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.players",
            "list",
            "Lists registered co-op players.",
            System.Array.Empty<IExpectedArgs>(),
            PlayerDebugCommands.List)
    {
    }
}

public interface IPlayerPartyStateCommand : ICoopCommand
{
}

public sealed class PlayerPartyStateCommand : LegacyCoopCommand, IPlayerPartyStateCommand
{
    public PlayerPartyStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.players",
            "party_state",
            "Reports replicated party state for a player.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
            },
            PlayerDebugCommands.PartyState)
    {
    }
}

public interface IPlayerDeleteCommand : ICoopCommand
{
}

public sealed class PlayerDeleteCommand : LegacyCoopCommand, IPlayerDeleteCommand
{
    public PlayerDeleteCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop",
            "delete_player",
            "Requests deletion of the local player from the server.",
            System.Array.Empty<IExpectedArgs>(),
            DeletePlayerCommand.DeletePlayer)
    {
    }
}
