using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event raised on the battle host each time an agent is spawned into a coop field
/// battle (postfix on <see cref="Mission.SpawnAgent"/>). The Missions battle controller turns it into a
/// replicated spawn so peers create a matching puppet. Only published on the host — see
/// <see cref="BattleSpawnGate"/>.
/// </summary>
public record AgentSpawnedInBattle : IEvent
{
    public Agent Agent { get; }

    public AgentSpawnedInBattle(Agent agent)
    {
        Agent = agent;
    }
}
