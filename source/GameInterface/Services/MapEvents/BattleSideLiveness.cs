using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Shared per-side liveness count for coop field battles: how many live human agents (own troops + puppets)
/// a given battle side currently has on the field. Both the depletion patch
/// (<c>CoopBattleDepletionPatch</c>) and the controller's end-condition hold
/// (<c>CoopBattleController.BattleReadyForEndChecks</c>) decide "is this side fielded" from this same count,
/// so the two mirrors cannot drift apart.
/// </summary>
public static class BattleSideLiveness
{
    /// <summary>
    /// Counts live human agents (<see cref="Agent.IsHuman"/> in each matching team's <c>ActiveAgents</c>)
    /// on <paramref name="side"/> across <paramref name="mission"/>'s teams. Puppets qualify — they join
    /// teams like any agent. Returns 0 for a null mission.
    /// </summary>
    public static int CountLiveHumanAgents(Mission mission, BattleSideEnum side)
    {
        if (mission == null) return 0;

        int active = 0;
        foreach (var team in mission.Teams)
        {
            if (team.Side != side) continue;
            foreach (var agent in team.ActiveAgents)
                if (agent.IsHuman) active++;
        }
        return active;
    }
}
