using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Villages.Commands;

public interface IAllowRaidAiInterventionCoopCommand : ICoopCommand
{
}

public sealed class AllowRaidAiInterventionCoopCommand : LegacyCoopCommand, IAllowRaidAiInterventionCoopCommand
{
    public AllowRaidAiInterventionCoopCommand()
        : base(
            "coop.debug.mapevent",
            "allow_raid_ai_intervention",
            "Controls raid ai intervention for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("mode", "The mode."),
            },
            RaidDebugCommands.AllowRaidAiIntervention)
    {
    }
}
