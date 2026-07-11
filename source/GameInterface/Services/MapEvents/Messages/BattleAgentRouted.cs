using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event raised when an agent routs out of a coop battle (postfix on
/// <see cref="Mission.OnAgentRemoved"/> with the routed state). The Missions battle controller decides,
/// from the agent's authority, whether this node owns the rout — if so it broadcasts it over the mesh so
/// every client despawns its puppet. Without this, peers keep routed puppets alive and their live-agent
/// depletion count never reaches zero, so the mission never ends for them.
/// </summary>
public record BattleAgentRouted : IEvent
{
    public Agent Agent { get; }

    public BattleAgentRouted(Agent agent)
    {
        Agent = agent;
    }
}
