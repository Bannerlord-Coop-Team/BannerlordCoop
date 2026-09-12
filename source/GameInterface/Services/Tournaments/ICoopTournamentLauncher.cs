using GameInterface.Services.Tournaments.Data;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Tournaments;

/// <summary>
/// Opens the shared coop tournament mission. Implemented by Missions so GameInterface does not reference that
/// assembly directly.
/// </summary>
public interface ICoopTournamentLauncher
{
    Mission OpenCoopTournament(TournamentSessionSnapshot snapshot, bool isSpectator);
}
