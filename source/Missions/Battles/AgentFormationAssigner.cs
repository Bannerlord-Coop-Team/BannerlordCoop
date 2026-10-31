using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Slots an agent into its team's formation for its troop class. Vanilla's Alt-hold/order-screen formation
/// marker VM (and order-targeting) key off <c>Formation.CountOfUnits</c>, which only counts members actually
/// assigned via <c>Agent.Formation</c> — an agent left off its formation is invisible to both even though
/// it's a real, fighting member of the side.
/// </summary>
public interface IAgentFormationAssigner
{
    /// <summary>
    /// Assign <paramref name="agent"/> to its team's formation for its character's troop class. Returns the
    /// formation (for a caller that needs it, e.g. to also call <c>SetControlledByAI</c>), or null if the
    /// agent has no character/team yet, its team has no matching formation or the formation cannot take it.
    /// </summary>
    Formation Assign(Agent agent);

    /// <summary>
    /// Assign <paramref name="agent"/> to the specific formation slot <paramref name="formationIndex"/> (a
    /// <see cref="FormationClass"/> cast to int) its owner placed it in, so a puppet mirrors the owner's actual
    /// deployment split rather than a default troop-class grouping. Falls back to the troop-class default when
    /// <paramref name="formationIndex"/> is negative (the owner's agent had no formation).
    /// </summary>
    Formation Assign(Agent agent, int formationIndex);
}

/// <inheritdoc cref="IAgentFormationAssigner"/>
public class AgentFormationAssigner : IAgentFormationAssigner
{
    public Formation Assign(Agent agent)
    {
        if (agent.Character == null || agent.Team == null) return null;

        return Join(agent, agent.Team.GetFormation(agent.Character.GetFormationClass()));
    }

    public Formation Assign(Agent agent, int formationIndex)
    {
        if (formationIndex < 0) return Assign(agent);
        if (agent.Team == null) return null;

        return Join(agent, agent.Team.GetFormation((FormationClass)formationIndex));
    }

    /// <summary>Whether <paramref name="formation"/> can take an agent now; a refused agent keeps its current formation.</summary>
    protected virtual bool CanJoin(Formation formation) => true;

    // Puts the agent in the formation when it may join; null leaves it where it was.
    private Formation Join(Agent agent, Formation formation)
    {
        if (formation == null || !CanJoin(formation)) return null;

        agent.Formation = formation;
        return formation;
    }
}
