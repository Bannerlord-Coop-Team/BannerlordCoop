using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>[Game thread, local] This client's AI now drives <see cref="Agent"/>, a puppet it adopted from another controller.</summary>
public readonly struct BattleAgentAdopted : IEvent
{
    public readonly Agent Agent;

    // Carries the agent the migrator just turned into this client's AI.
    public BattleAgentAdopted(Agent agent)
    {
        Agent = agent;
    }
}
