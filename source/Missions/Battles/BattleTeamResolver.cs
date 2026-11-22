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

    /// <summary>
    /// [Game thread] The team of an AI fleet on <paramref name="side"/>, whoever simulates it: the one every peer puts that
    /// fleet's copies on, so the battle host's own allies go on the side's ally team and never on <c>PlayerTeam</c>.
    /// </summary>
    Team ResolveAiFleetTeam(BattleSideEnum side);
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

    // A copy of an AI hull is another owner's record wherever it lands, so the simulating host uses that same team.
    public Team ResolveAiFleetTeam(BattleSideEnum side) => ResolveReplicatedTeam(side, isOwn: false);

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
