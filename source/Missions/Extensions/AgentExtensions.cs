using Missions.Network;
using TaleWorlds.MountAndBlade;

namespace Missions.Extensions
{
    public static class AgentExtensions
    {
        public static bool IsNetworkAgent(this Agent agent)
        {
            return NetworkAgentRegistry.Instance.AgentToId.ContainsKey(agent);
        }
    }
}
