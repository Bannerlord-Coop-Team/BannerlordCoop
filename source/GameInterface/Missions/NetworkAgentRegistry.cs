using Common.Logging;
using GameInterface.Missions.Agents.Packets;
using LiteNetLib;
using Serilog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Services.Network
{
    /// <summary>
    /// Agent Registry for associating Agents over the network
    /// </summary>
    public interface INetworkAgentRegistry
    {
        /// <summary>
        /// Used to easily convert Agent to Id
        /// </summary>
        /// <remarks>
        /// Works with controlled and non-controlled agents
        /// </remarks>
        IReadOnlyDictionary<Agent, string> AgentToId { get; }
        /// <summary>
        /// Agents directly controlled by the client
        /// </summary>
        IReadOnlyDictionary<string, Agent> ControlledAgents { get; }
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
        bool RegisterControlledAgent(string controllerId, Agent agent);

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
        bool RegisterNetworkControlledAgent(NetPeer peer, string agentId, Agent agent);

        /// <summary>
        /// Remove a controlled agent.
        /// </summary>
        /// <param name="agentId">Agent id to remove</param>
        /// <returns>True if removal was successful, false otherwise</returns>
        bool RemoveControlledAgent(string agentId);

        /// <summary>
        /// Remove a network controlled agent.
        /// </summary>
        /// <param name="agentId">Agent id to remove</param>
        /// <returns>True if removal was successful, false otherwise</returns>
        bool RemoveNetworkControlledAgent(string agentId);

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
        bool IsControlled(string guid);

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
        bool IsAgentRegistered(string guid);

        /// <summary>
        /// Try to get the Agent guid from the Agent
        /// </summary>
        /// <param name="agent">Agent to check for guid</param>
        /// <returns>True if Agent guid is found and assigns guid, false otherwise</returns>
        bool TryGetAgentId(Agent agent, out string guid);

        /// <summary>
        /// Try to get the Agent from a guid
        /// </summary>
        /// <param name="guid">guid to check for Agent</param>
        /// <returns>True if guid is found and assigns agent, false otherwise</returns>
        bool TryGetAgent(string guid, out Agent agent);

        /// <summary>
        /// Try to get the Agent from a guid
        /// </summary>
        /// <param name="guid">guid to check for Agent</param>
        /// <returns>True if guid is found and assigns agent, false otherwise</returns>
        bool TryGetGroupController(NetPeer peer, out AgentGroupController agentGroupController);

        /// <summary>
        /// Attempts to get the controlling peer of a given agent
        /// </summary>
        /// <remarks>
        /// This will fail if the agent is controlled internally
        /// </remarks>
        /// <param name="agent">Agent to get controller</param>
        /// <param name="controllerPeer">Controlling peer</param>
        /// <returns>True if retrieval of controlling peer was successful, otherwise False</returns>
        bool TryGetExternalController(Agent agent, out NetPeer controllerPeer);

        /// <summary>
        /// Attempts to get the controlling peer of a given agent
        /// </summary>
        /// <remarks>
        /// This will fail if the agent is controlled internally
        /// </remarks>
        /// <param name="agentId">AgentId to get controller</param>
        /// <param name="controllerPeer">Controlling peer</param>
        /// <returns>True if retrieval of controlling peer was successful, otherwise False</returns>
        bool TryGetExternalController(string agentId, out NetPeer controllerPeer);
    }

    /// <inheritdoc cref="INetworkAgentRegistry"/>
    public class NetworkAgentRegistry : INetworkAgentRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NetworkAgentRegistry>();
        public static INetworkAgentRegistry Instance 
        { 
            get
            {
                if(_instance == null)
                {
                    _instance = new NetworkAgentRegistry();
                }

                return _instance;
            } 
        }
        private static INetworkAgentRegistry _instance;

        /// <inheritdoc/>
        public IReadOnlyDictionary<Agent, string> AgentToId => _agentToId;
        private readonly ConcurrentDictionary<Agent, string> _agentToId = new ConcurrentDictionary<Agent, string>();

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Agent> ControlledAgents => _controlledAgents;
        private readonly ConcurrentDictionary<string, Agent> _controlledAgents = new ConcurrentDictionary<string, Agent>();

        /// <inheritdoc/>
        public IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents => _otherAgents;
        private readonly ConcurrentDictionary<NetPeer, AgentGroupController> _otherAgents = new ConcurrentDictionary<NetPeer, AgentGroupController>();

        /// <inheritdoc/>
        public bool RegisterControlledAgent(string controllerId, Agent agent)
        {
            if (agent == null) return false;

            if (_agentToId.ContainsKey(agent)) return false;
            if (_controlledAgents.ContainsKey(controllerId)) return false;

            _controlledAgents.TryAdd(controllerId, agent);
            _agentToId.TryAdd(agent, controllerId);

            return true;
        }

        /// <inheritdoc/>
        public bool RegisterNetworkControlledAgent(NetPeer peer, string agentId, Agent agent)
        {
            if (agent == null) return false;

            if (_otherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                if (controller.Contains(agent)) return false;
                if (controller.Contains(agentId)) return false;

                controller.AddAgent(agentId, agent);
                _agentToId.TryAdd(agent, agentId);
            }
            else
            {
                AgentGroupController newGroupController = new AgentGroupController(peer);
                newGroupController.AddAgent(agentId, agent);
                _agentToId.TryAdd(agent, agentId);
                _otherAgents.TryAdd(peer, newGroupController);
            }

            return true;
        }

        /// <inheritdoc/>
        public bool RemoveControlledAgent(string agentId)
        {
            if (_controlledAgents.TryGetValue(agentId, out Agent agent))
            {
                _controlledAgents.TryRemove(agentId, out var _);
                return _agentToId.TryRemove(agent, out var _);
            }
            return false;
        }

        /// <inheritdoc/>
        public bool RemoveNetworkControlledAgent(string agentId)
        {
            return _otherAgents.Values.Any(group =>
            {
                Agent agent = group.RemoveAgent(agentId);
                if (agent != null)
                {
                    return _agentToId.TryRemove(agent, out var _);
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
                result &= controller.ControlledAgents.All(kvp => _agentToId.TryRemove(kvp.Value, out var _));
                result &= _otherAgents.TryRemove(peer, out var _);
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
            if (agent == null) return false;

            if (AgentToId.TryGetValue(agent, out var id) == false) return false;

            if (ControlledAgents.ContainsKey(id)) { return true; }
            return false;
        }

        /// <inheritdoc/>
        public bool IsControlled(string agentId)
        {
            if (ControlledAgents.ContainsKey(agentId)) { return true; }
            return false;
        }

        /// <inheritdoc/>
        public bool IsAgentRegistered(Agent agent)
        {
            if (agent == null) return false;

            return AgentToId.ContainsKey(agent);
        }

        /// <inheritdoc/>
        public bool IsAgentRegistered(string guid)
        {
            if (ControlledAgents.ContainsKey(guid)) { return true; }

            foreach (AgentGroupController controller in OtherAgents.Values)
            {
                if (controller.Contains(guid)) { return true; }
            }

            return false;
        }

        /// <inheritdoc/>
        public bool TryGetAgentId(Agent agent, out string guid)
        {
            guid = default;
            if (agent == null) return false;

            if (AgentToId.TryGetValue(agent, out string agentId))
            {
                guid = agentId;
                return true;
            }
            guid = default;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetAgent(string guid, out Agent agent)
        {
            if (ControlledAgents.TryGetValue(guid, out Agent resolvedAgent))
            {
                agent = resolvedAgent;
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
            if (OtherAgents.TryGetValue(peer, out AgentGroupController resolvedController))
            {
                agentGroupController = resolvedController;
                return true;
            }
            agentGroupController = default;
            return false;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _controlledAgents.Clear();
            _otherAgents.Clear();
            _agentToId.Clear();
        }

        /// <inheritdoc/>
        public bool TryGetExternalController(Agent agent, out NetPeer controllerPeer)
        {
            controllerPeer = default;

            if(_agentToId.TryGetValue(agent, out var agentId) == false)
            {
                Logger.Verbose("Unable to find agent in {idList} in method {method}", nameof(_agentToId), nameof(TryGetExternalController));
                return false;
            }

            return TryGetExternalController(agentId, out controllerPeer);
        }

        /// <inheritdoc/>
        public bool TryGetExternalController(string agentId, out NetPeer controllerPeer)
        {
            controllerPeer = default;

            if (_controlledAgents.ContainsKey(agentId))
            {
                return false;
            }

            foreach (AgentGroupController controller in OtherAgents.Values)
            {
                if (controller.Contains(agentId))
                {
                    controllerPeer = controller.ControllingPeer;
                    return true;
                }
            }

            return false;
        }
    }
}
