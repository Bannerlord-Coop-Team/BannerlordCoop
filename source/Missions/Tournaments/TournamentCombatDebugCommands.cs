#if DEBUG
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Tournaments;

internal static class TournamentCombatDebugCommands
{
    [CommandLineArgumentFunction("combat_fixture_state", "coop.debug.tournaments")]
    public static string GetCombatFixtureState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.tournaments.combat_fixture_state";

        CoopTournamentController controller =
            Mission.Current?.GetMissionBehavior<CoopTournamentController>();
        return controller?.GetCombatFixtureState() ?? "No active co-op tournament mission";
    }
}
#endif
