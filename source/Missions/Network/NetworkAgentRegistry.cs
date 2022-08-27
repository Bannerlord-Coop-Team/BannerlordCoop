using LiteNetLib;
using Missions.Packets.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Network
{
    public class NetworkAgentRegistry
    {
        public static IReadOnlyDictionary<Agent, Guid> AgentToId => m_AgentToId;
        private static readonly Dictionary<Agent, Guid> m_AgentToId = new Dictionary<Agent, Guid>();

        public static IReadOnlyDictionary<Guid, Agent> ControlledAgents => m_ControlledAgents;
        private static readonly Dictionary<Guid, Agent> m_ControlledAgents = new Dictionary<Guid, Agent>();

        public static IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents => m_OtherAgents;
        private static readonly Dictionary<NetPeer, AgentGroupController> m_OtherAgents = new Dictionary<NetPeer, AgentGroupController>();

        public static bool RegisterControlledAgent(Guid agentId, Agent agent)
        {
            if (m_AgentToId.ContainsKey(agent)) return false;
            if (m_ControlledAgents.ContainsKey(agentId)) return false;

            m_ControlledAgents.Add(agentId, agent);
            m_AgentToId.Add(agent, agentId);

            return true;
        }

        public static bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent)
        {
            if (m_OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                if (controller.Contains(agent)) return false;
                if (controller.Contains(agentId)) return false;

                controller.AddAgent(agentId, agent);
                m_AgentToId.Add(agent, agentId);
            }
            else
            {
                AgentGroupController newGroupController = new AgentGroupController();
                newGroupController.AddAgent(agentId, agent);
                m_AgentToId.Add(agent, agentId);
                m_OtherAgents.Add(peer, newGroupController);
            }

            return true;
        }

        public static bool RemoveControlledAgent(Guid agentId)
        {
            if (m_ControlledAgents.TryGetValue(agentId, out Agent agent))
            {
                return m_AgentToId.Remove(agent);
            }
            return false;
        }

        public static bool RemoveNetworkControlledAgent(Guid agentId)
        {
            return m_OtherAgents.Values.Any(group =>
            {
                Agent agent = group.RemoveAgent(agentId);
                if (agent != null)
                {
                    return m_AgentToId.Remove(agent);
                }
                else
                {
                    return false;
                }
            });
        }

        public static bool RemovePeer(NetPeer peer)
        {
            bool result = true;
            if (m_OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                result &= controller.ControlledAgents.All(kvp => m_AgentToId.Remove(kvp.Value));
                result &= m_OtherAgents.Remove(peer);
            }
            else
            {
                result &= false;
            }
            return result;
        }

        public static void Clear()
        {
            m_AgentToId.Clear();
            m_OtherAgents.Clear();
            m_AgentToId.Clear();
        }
    }
}
