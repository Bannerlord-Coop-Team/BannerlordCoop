using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Extensions
{
    public static class AgentExtensions
    {
        public static bool IsNetworkAgent(this Agent agent) 
        {
            return NetworkAgentRegistry.Instance.AgentToId.ContainsKey(agent);
        }

        public static bool IsPlayerAgent(this Agent agent)
        {
            if (NetworkAgentRegistry.Instance.AgentToId.TryGetValue(agent, out Guid id))
            {
                return NetworkAgentRegistry.Instance.PlayerAgents.ContainsKey(id);
            }

            return false;
        }
    }
}
