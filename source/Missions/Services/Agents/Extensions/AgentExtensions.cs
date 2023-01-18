using TaleWorlds.MountAndBlade;
using Missions.Services.Network;
using Missions.Services.Agents.Packets;

namespace Missions.Services.Agents.Extensions
{
    public static class AgentExtensions
    {
        public static bool IsNetworkAgent(this Agent agent)
        {
            return NetworkAgentRegistry.Instance.AgentToId.ContainsKey(agent);
        }

        /// <summary>
        /// Determines if the agent's movement has changed since last time the <see cref="AgentData"/> packet has been sent.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public static bool HasMovementUpdated(this Agent agent, AgentData agentData)
        {
            return agentData.InputVector != agent.MovementInputVector
                || agentData.LookDirection != agent.LookDirection
                || !agentData.ActionData.Equals(new AgentActionData(agent))
                || !agentData.MountData.Equals(new AgentMountData(agent));
        }
    }
}
