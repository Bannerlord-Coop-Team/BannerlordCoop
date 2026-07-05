using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Slots an agent into its team's formation for its troop class. Vanilla's Alt-hold/order-screen formation
/// marker VM (and order-targeting) key off <c>Formation.CountOfUnits</c>, which only counts members actually
/// assigned via <c>Agent.Formation</c> — an agent left off its formation is invisible to both even though
/// it's a real, fighting member of the side.
/// </summary>
public static class AgentFormationAssigner
{
    /// <summary>
    /// Assign <paramref name="agent"/> to its team's formation for its character's troop class. Returns the
    /// formation (for a caller that needs it, e.g. to also call <c>SetControlledByAI</c>), or null if the
    /// agent has no character/team yet or its team has no matching formation.
    /// </summary>
    public static Formation Assign(Agent agent)
    {
        if (agent.Character == null || agent.Team == null) return null;

        var formation = agent.Team.GetFormation(agent.Character.GetFormationClass());
        if (formation != null)
            agent.Formation = formation;

        return formation;
    }
}
