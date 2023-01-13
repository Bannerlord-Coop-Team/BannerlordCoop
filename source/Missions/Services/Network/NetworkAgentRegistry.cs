using Common;
using Common.Messaging;
using LiteNetLib;
using Missions.Services.Agents.Extensions;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network
{
    public interface INetworkAgentRegistry
    {
        IReadOnlyDictionary<Agent, Guid> AgentToId { get; }
        IReadOnlyDictionary<Guid, Agent> ControlledAgents { get; }
        IReadOnlyDictionary<Guid, Agent> PlayerAgents { get; }
        IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents { get; }

        void Clear();
        bool RegisterControlledAgent(Guid agentId, Agent agent);
        bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent);
        bool RegisterPlayerAgent(Guid agentId, Agent agent);
        bool RegisterNetworkPlayerAgent(NetPeer peer, Guid agentId, Agent agent);
        bool RemoveControlledAgent(Guid agentId);
        bool RemovePlayerAgent(Guid agentId);
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

        public IReadOnlyDictionary<Guid, Agent> PlayerAgents => m_PlayerAgents;
        private readonly Dictionary<Guid, Agent> m_PlayerAgents = new Dictionary<Guid, Agent>();

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
        private bool RemoveNetworkControlledAgent(Guid agentId)
        {
            return m_OtherAgents.Values.Any(group =>
            {
                Agent agent = group.RemoveAgent(agentId);
                if (agent != null)
                {
                    var result = m_AgentToId.Remove(agent);

                    if (m_PlayerAgents.ContainsKey(agentId))
                    {
                        result &= m_PlayerAgents.Remove(agentId);
                    }

                    return result;
                }
                else
                {
                    return false;
                }
            });
        }

        public bool RegisterControlledAgent(Guid agentId, Agent agent)
        {
            if (m_AgentToId.ContainsKey(agent) ||
                m_ControlledAgents.ContainsKey(agentId)) return false;

            m_ControlledAgents.Add(agentId, agent);
            m_AgentToId.Add(agent, agentId);

            return true;
        }

        public bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent)
        {
            if (m_OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                if (controller.Contains(agent) || controller.Contains(agentId)) return false;

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

        public bool RegisterPlayerAgent(Guid agentId, Agent agent)
        {
            if (m_AgentToId.ContainsKey(agent) ||
                m_PlayerAgents.ContainsKey(agentId)) return false;

            m_PlayerAgents.Add(agentId, agent);
            m_AgentToId.Add(agent, agentId);

            return true;
        }

        public bool RegisterNetworkPlayerAgent(NetPeer peer, Guid agentId, Agent agent)
        {
            if (m_OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                if (controller.Contains(agent) || controller.Contains(agentId)) return false;

                controller.AddAgent(agentId, agent);
                m_AgentToId.Add(agent, agentId);
                m_PlayerAgents.Add(agentId, agent);
            }
            else
            {
                AgentGroupController newGroupController = new AgentGroupController();
                newGroupController.AddAgent(agentId, agent);
                m_AgentToId.Add(agent, agentId);
                m_OtherAgents.Add(peer, newGroupController);
                m_PlayerAgents.Add(agentId, agent);
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

        public bool RemovePlayerAgent(Guid agentId)
        {
            if (m_PlayerAgents.TryGetValue(agentId, out Agent agent))
            {
                return m_AgentToId.Remove(agent);
            }

            return false;
        }

        public bool RemovePeer(NetPeer peer)
        {
            bool result = true;
            if (m_OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                result &= controller.ControlledAgents.All(kvp => 
                {
                    var innerResult = m_AgentToId.Remove(kvp.Value);

                    if (kvp.Value.IsPlayerAgent())
                    {
                        innerResult &= m_PlayerAgents.Remove(kvp.Key);
                    }

                    return innerResult;
                });
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
            m_PlayerAgents.Clear();
        }
    }
}
