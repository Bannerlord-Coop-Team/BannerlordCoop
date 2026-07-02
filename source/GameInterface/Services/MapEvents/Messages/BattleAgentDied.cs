using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event raised when an agent dies in a coop field battle (postfix on
/// <see cref="Agent.Die"/>). The Missions battle controller decides, from the agent's authority, whether
/// this node owns the death — if so it broadcasts it over the mesh so every client kills its puppet. Fires
/// for every death (owner agents and puppets alike); the authority check in the controller filters it.
/// </summary>
public record BattleAgentDied : IEvent
{
    public Agent Agent { get; }

    /// <summary>True when the agent fell unconscious (wounded) rather than being killed outright.</summary>
    public bool Wounded { get; }

    public BattleAgentDied(Agent agent, bool wounded)
    {
        Agent = agent;
        Wounded = wounded;
    }
}
