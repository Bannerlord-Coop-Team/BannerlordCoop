using Common.Messaging;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;
using Missions.Services.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;

namespace Missions.Services.Network
{
    public interface INetworkAgentRegistry
    {
        IReadOnlyDictionary<Agent, Guid> AgentToId { get; }
        IReadOnlyDictionary<Guid, Agent> ControlledAgents { get; }
        IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents { get; }

        void Clear();
        bool RegisterControlledAgent(Guid agentId, Agent agent);
        bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent);
        bool RemoveControlledAgent(Guid agentId);
        bool RemovePeer(NetPeer peer);
    }

    public class NetworkAgentRegistry : INetworkAgentRegistry
    {
        public static INetworkAgentRegistry Instance 
        { 
            get
            {
                if(_instance == null)
                {
                    _instance = new NetworkAgentRegistry(MessageBroker.Instance);
                }

                return _instance;
            } 
        }
        private static INetworkAgentRegistry _instance;

        public IReadOnlyDictionary<Agent, Guid> AgentToId => m_AgentToId;
        private readonly Dictionary<Agent, Guid> m_AgentToId = new Dictionary<Agent, Guid>();

        public IReadOnlyDictionary<Guid, Agent> ControlledAgents => m_ControlledAgents;
        private readonly Dictionary<Guid, Agent> m_ControlledAgents = new Dictionary<Guid, Agent>();

        public IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents => m_OtherAgents;
        private readonly Dictionary<NetPeer, AgentGroupController> m_OtherAgents = new Dictionary<NetPeer, AgentGroupController>();
        private readonly IMessageBroker _messageBroker;

        public NetworkAgentRegistry(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
            _messageBroker.Subscribe<AgentDeleted>(Handle_AgentDeleted);
        }

        private void Handle_AgentDeleted(MessagePayload<AgentDeleted> payload)
        {
            Agent affectedAgent = payload.What.Agent;
            if (AgentToId.TryGetValue(affectedAgent, out Guid agentId))
            {
                RemoveNetworkControlledAgent(agentId);
            }
        }

        public bool RegisterControlledAgent(Guid agentId, Agent agent)
        {
            if (m_AgentToId.ContainsKey(agent)) return false;
            if (m_ControlledAgents.ContainsKey(agentId)) return false;

            m_ControlledAgents.Add(agentId, agent);
            m_AgentToId.Add(agent, agentId);

            return true;
        }

        public bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent)
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

        public bool RemoveControlledAgent(Guid agentId)
        {
            if (m_ControlledAgents.TryGetValue(agentId, out Agent agent))
            {
                return m_AgentToId.Remove(agent);
            }
            return false;
        }

        public bool RemoveNetworkControlledAgent(Guid agentId)
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

        public bool RemovePeer(NetPeer peer)
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

        public void Clear()
        {
            m_AgentToId.Clear();
            m_OtherAgents.Clear();
            m_AgentToId.Clear();
        }
    }
}
