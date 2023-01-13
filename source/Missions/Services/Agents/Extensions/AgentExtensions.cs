using TaleWorlds.MountAndBlade;
using Missions.Services.Network;
using System.Linq;

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
            return NetworkAgentRegistry.Instance.PlayerAgents.Any(kvp => kvp.Value == agent);
        }
    }
}
