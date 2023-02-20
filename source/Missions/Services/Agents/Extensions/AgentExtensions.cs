using TaleWorlds.MountAndBlade;
using Missions.Services.Network;

namespace Missions.Services.Agents.Extensions
{
    public static class AgentExtensions
    {
        public static bool IsNetworkAgent(this Agent agent)
        {
            return NetworkAgentRegistry.Instance.AgentToId.ContainsKey(agent);
        }
    }
}
