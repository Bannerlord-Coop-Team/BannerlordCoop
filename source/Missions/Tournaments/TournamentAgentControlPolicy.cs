using GameInterface.Services.Tournaments.Data;

namespace Missions.Tournaments;

public enum TournamentAgentControlRole
{
    Puppet = 0,
    HumanPlayer = 1,
    NpcAuthority = 2
}

public static class TournamentAgentControlPolicy
{
    public static TournamentAgentControlRole Resolve(
        TournamentContestantData contestant,
        string manifestControllerId,
        string ownControllerId)
    {
        if (contestant == null || manifestControllerId != ownControllerId)
            return TournamentAgentControlRole.Puppet;
        return contestant.IsHuman && !contestant.IsReplaced
            ? TournamentAgentControlRole.HumanPlayer
            : TournamentAgentControlRole.NpcAuthority;
    }
}
