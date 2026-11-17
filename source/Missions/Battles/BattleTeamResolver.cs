using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Picks the local team for another owner's replicated agents and hulls.</summary>
public interface IBattleTeamResolver
{
    /// <summary>
    /// [Game thread] The team for a replicated record on <paramref name="side"/>: our own records go to the side's
    /// main team, another owner's never to <c>PlayerTeam</c>. Null while the teams are not created yet.
    /// </summary>
    Team ResolveReplicatedTeam(BattleSideEnum side, bool isOwn);
}

/// <inheritdoc cref="IBattleTeamResolver"/>
public class BattleTeamResolver : IBattleTeamResolver
{
    public Team ResolveReplicatedTeam(BattleSideEnum side, bool isOwn)
    {
        var mission = Mission.Current;
        if (mission == null) return null;

        var allyTeam = side == BattleSideEnum.Attacker ? mission.AttackerAllyTeam : mission.DefenderAllyTeam;
        return Choose(BattleTeams.Resolve(side), allyTeam, mission.PlayerTeam, isOwn);
    }

    // Every formation on PlayerTeam is locally commandable, so another owner's records go to the side's ally
    // team when the side's main team is ours; with no such team they wait.
    internal static T Choose<T>(T mainTeam, T allyTeam, T playerTeam, bool isOwn) where T : class
    {
        if (mainTeam == null) return null;
        if (isOwn || mainTeam != playerTeam) return mainTeam;
        if (allyTeam != null && allyTeam != playerTeam) return allyTeam;
        return null;
    }
}
