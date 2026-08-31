using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Towns.Commands;

public interface IAuditorCoopCommand : ICoopCommand
{
}

public sealed class AuditorCoopCommand : LegacyCoopCommand, IAuditorCoopCommand
{
    public AuditorCoopCommand()
        : base(
            "coop.debug.town",
            "auditor",
            "Runs the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TownAuditorDebugCommand.Auditor)
    {
    }
}
