using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Messages
{
    /// <summary>
    /// Internal event for Agent deaths
    /// </summary>
    internal readonly struct AgentDied : IEvent
    {
        public Agent Agent { get; }

        public AgentDied(Agent agent)
        {
            Agent = agent;
        }
    }
}