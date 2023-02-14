using Common;
using Common.Messaging;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;
using Missions.Services.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Serilog;
using Common.Logging;
using TaleWorlds.Core;
using System.Security.Policy;

namespace Missions.Services.Network
{
    public interface INetworkAgentRegistry
    {
        /// <summary>
        /// Used to easily convert Agent to Id
        /// </summary>
        /// <remarks>
        /// Works with controlled and non-controlled agents
        /// </remarks>
        IReadOnlyDictionary<Agent, Guid> AgentToId { get; }
        /// <summary>
        /// Agents directly controlled by the client
        /// </summary>
        IReadOnlyDictionary<Guid, Agent> ControlledAgents { get; }
        /// <summary>
        /// Agents not controlled by the client.
        /// These are controlled by network events.
        /// </summary>
        IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents { get; }

        /// <summary>
        /// Clears all data
        /// </summary>
        void Clear();

        /// <summary>
        /// Register a controlled agent.
        /// </summary>
        /// <remarks>
        /// A controlled agent is meant to be managed by this
        /// client.
        /// </remarks>
        /// <param name="agentId">Id to assign the agent</param>
        /// <param name="agent">Agent to add</param>
        /// <returns>True if addition was successful, false otherwise</returns>
        bool RegisterControlledAgent(Guid agentId, Agent agent);

        /// <summary>
        /// Register a network controlled agent.
        /// </summary>
        /// <remarks>
        /// A network controlled agent is meant to be managed across
        /// the network.
        /// </remarks>
        /// <param name="peer">Peer that will control this agent</param>
        /// <param name="agentId">Id assigned to the agent</param>
        /// <param name="agent">Agent to register</param>
        /// <returns>True if addition was successful, false otherwise</returns>
        bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent);

        /// <summary>
        /// Remove a controlled agent.
        /// </summary>
        /// <param name="agentId">Agent id to remove</param>
        /// <returns>True if removal was successful, false otherwise</returns>
        bool RemoveControlledAgent(Guid agentId);

        /// <summary>
        /// Remove peer from network controllers.
        /// This removes all controlled agents under this peer.
        /// </summary>
        /// <param name="peer">Peer to remove</param>
        /// <returns>True if removal was successful, false otherwise</returns>
        bool RemovePeer(NetPeer peer);

        /// <summary>
        /// Is agent controlled locally?
        /// </summary>
        /// <param name="agent">Agent to check if controlled</param>
        /// <returns>True if agent is controlled locally, false otherwise</returns>
        bool IsControlled(Agent agent);

        /// <summary>
        /// Is Agent guid controlled locally?
        /// </summary>
        /// <param name="guid">Agent guid to check if controlled</param>
        /// <returns>True if Agent guid is controlled locally, false otherwise</returns>
        bool IsControlled(Guid guid);

        /// <summary>
        /// Is Agent registered?
        /// </summary>
        /// <param name="agent">Agent to check if registered</param>
        /// <returns>True if Agent is registered, false otherwise</returns>
        bool IsAgentRegistered(Agent agent);

        /// <summary>
        /// Is Agent guid registered?
        /// </summary>
        /// <param name="guid">Agent guid to check if registered</param>
        /// <returns>True if Agent guid is registered, false otherwise</returns>
        bool IsAgentRegistered(Guid guid);

        /// <summary>
        /// Try to get the Agent guid from the Agent
        /// </summary>
        /// <param name="agent">Agent to check for guid</param>
        /// <returns>True if Agent guid is found and assigns guid, false otherwise</returns>
        bool TryGetAgentId(Agent agent, out Guid guid);

        /// <summary>
        /// Try to get the Agent from a guid
        /// </summary>
        /// <param name="guid">guid to check for Agent</param>
        /// <returns>True if guid is found and assigns agent, false otherwise</returns>
        bool TryGetAgent(Guid guid, out Agent agent);

        /// <summary>
        /// Try to get the Agent from a guid
        /// </summary>
        /// <param name="guid">guid to check for Agent</param>
        /// <returns>True if guid is found and assigns agent, false otherwise</returns>
        bool TryGetGroupController(NetPeer peer, out AgentGroupController agentGroupController);
    }

    /// <inheritdoc/>
    public class NetworkAgentRegistry : INetworkAgentRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NetworkAgentRegistry>();
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

        /// <inheritdoc/>
        public IReadOnlyDictionary<Agent, Guid> AgentToId => _agentToId;
        private readonly Dictionary<Agent, Guid> _agentToId = new Dictionary<Agent, Guid>();

        /// <inheritdoc/>
        public IReadOnlyDictionary<Guid, Agent> ControlledAgents => _controlledAgents;
        private readonly Dictionary<Guid, Agent> _controlledAgents = new Dictionary<Guid, Agent>();

        /// <inheritdoc/>
        public IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents => _otherAgents;
        private readonly Dictionary<NetPeer, AgentGroupController> _otherAgents = new Dictionary<NetPeer, AgentGroupController>();
        private readonly IMessageBroker _messageBroker;

        public NetworkAgentRegistry(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
            _messageBroker.Subscribe<AgentDeleted>(Handle_AgentDeleted);
        }

        // TODO move to different file
        private void Handle_AgentDeleted(MessagePayload<AgentDeleted> payload)
        {
            Agent affectedAgent = payload.What.Agent;
            if (AgentToId.TryGetValue(affectedAgent, out Guid agentId))
            {
                RemoveNetworkControlledAgent(agentId);
            }
        }

        /// <inheritdoc/>
        public bool RegisterControlledAgent(Guid agentId, Agent agent)
        {
            if (_agentToId.ContainsKey(agent)) return false;
            if (_controlledAgents.ContainsKey(agentId)) return false;

            _controlledAgents.Add(agentId, agent);
            _agentToId.Add(agent, agentId);

            return true;
        }

        /// <inheritdoc/>
        public bool RegisterNetworkControlledAgent(NetPeer peer, Guid agentId, Agent agent)
        {
            if (_otherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                if (controller.Contains(agent)) return false;
                if (controller.Contains(agentId)) return false;

                controller.AddAgent(agentId, agent);
                _agentToId.Add(agent, agentId);
            }
            else
            {
                AgentGroupController newGroupController = new AgentGroupController();
                newGroupController.AddAgent(agentId, agent);
                _agentToId.Add(agent, agentId);
                _otherAgents.Add(peer, newGroupController);
            }

            return true;
        }

        /// <inheritdoc/>
        public bool RemoveControlledAgent(Guid agentId)
        {
            if (_controlledAgents.TryGetValue(agentId, out Agent agent))
            {
                return _agentToId.Remove(agent);
            }
            return false;
        }

        /// <inheritdoc/>
        public bool RemoveNetworkControlledAgent(Guid agentId)
        {
            return _otherAgents.Values.Any(group =>
            {
                Agent agent = group.RemoveAgent(agentId);
                if (agent != null)
                {
                    return _agentToId.Remove(agent);
                }
                else
                {
                    return false;
                }
            });
        }

        /// <inheritdoc/>
        public bool RemovePeer(NetPeer peer)
        {
            bool result = true;
            if (_otherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                result &= controller.ControlledAgents.All(kvp => _agentToId.Remove(kvp.Value));
                result &= _otherAgents.Remove(peer);
            }
            else
            {
                result &= false;
            }
            return result;
        }

        /// <inheritdoc/>
        public bool IsControlled(Agent agent)
        {
            if (ControlledAgents.ContainsKey(AgentToId[agent])) { return true; }
            return false;
        }

        /// <inheritdoc/>
        public bool IsControlled(Guid agentId)
        {
            if (ControlledAgents.ContainsKey(agentId)) { return true; }
            return false;
        }

        /// <inheritdoc/>
        public bool IsAgentRegistered(Agent agent)
        {
            return AgentToId.ContainsKey(agent);
        }

        /// <inheritdoc/>
        public bool IsAgentRegistered(Guid guid)
        {
            if (ControlledAgents.ContainsKey(guid)) { return true; }

            foreach (AgentGroupController controller in OtherAgents.Values)
            {
                if (controller.Contains(guid)) { return true; }
            }

            return false;
        }

        /// <inheritdoc/>
        public bool TryGetAgentId(Agent agent, out Guid guid)
        {
            if (AgentToId.TryGetValue(agent, out Guid agendId)
            {
                guid = agentId;
                return true;
            }
            guid = default(Guid);
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetAgent(Guid guid, out Agent agent)
        {
            if (IsControlled(guid))
            {
                agent = ControlledAgents[guid];
                return true;
            }
            if (IsAgentRegistered(guid))
            {
                foreach (AgentGroupController controller in OtherAgents.Values)
                {
                    if (controller.Contains(guid))
                    {
                        agent = controller.ControlledAgents[guid];
                        return true;
                    }
                }
            }
            agent = default;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetGroupController(NetPeer peer, out AgentGroupController agentGroupController)
        {
            if (OtherAgents.ContainsKey(peer))
            {
                agentGroupController = OtherAgents[peer];
            }
            agentGroupController = null;
            return false;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _agentToId.Clear();
            _otherAgents.Clear();
            _agentToId.Clear();
        }
    }
}
