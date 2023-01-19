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
    }
}
