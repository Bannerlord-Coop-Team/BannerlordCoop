using System.Collections.Generic;
using Common.Logging;
using Missions.Tournaments;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Coop
{
    public class TournamentSpectatorDebugCommand
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TournamentSpectatorDebugCommand>();

        [CommandLineArgumentFunction("get_last_spectator_spawnpoint", "coop.debug.tournaments")]
        public static string GetLastSpectatorSpawnpoint(List<string> args)
        {
            if (args.Count != 0)
                return DisplayResult("Usage: coop.debug.tournaments.get_last_spectator_spawnpoint");

            CoopTournamentController controller =
                Mission.Current?.GetMissionBehavior<CoopTournamentController>();
            return DisplayResult(
                controller?.LastLocalSpectatorSpawnName ??
                "No local spectator spawnpoint has been recorded in this mission.");
        }

        private static string DisplayResult(string result)
        {
            Logger.Information("[TournamentSpectator] Debug command result: {Result}", result);
            InformationManager.DisplayMessage(new InformationMessage(result));
            return result;
        }
    }
}