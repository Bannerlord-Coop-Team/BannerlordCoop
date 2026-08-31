using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Arenas.Commands;

public interface IViewArenaMasterInteractionsCommandCoopCommand : ICoopCommand
{
}

public sealed class ViewArenaMasterInteractionsCommandCoopCommand : LegacyCoopCommand, IViewArenaMasterInteractionsCommandCoopCommand
{
    public ViewArenaMasterInteractionsCommandCoopCommand()
        : base(
            "coop.debug.arenas",
            "list_interactions",
            "Lists interactions for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            ArenaMasterCommands.ViewArenaMasterInteractionsCommand)
    {
    }
}
