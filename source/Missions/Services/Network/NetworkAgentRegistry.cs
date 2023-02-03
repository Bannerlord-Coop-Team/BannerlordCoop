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

        public IReadOnlyDictionary<Agent, Guid> AgentToId => _agentToId;
        private readonly Dictionary<Agent, Guid> _agentToId = new Dictionary<Agent, Guid>();

        public IReadOnlyDictionary<Guid, Agent> ControlledAgents => _controlledAgents;
        private readonly Dictionary<Guid, Agent> _controlledAgents = new Dictionary<Guid, Agent>();

        public IReadOnlyDictionary<NetPeer, AgentGroupController> OtherAgents => _otherAgents;
        private readonly Dictionary<NetPeer, AgentGroupController> _otherAgents = new Dictionary<NetPeer, AgentGroupController>();
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
            if (_agentToId.ContainsKey(agent)) return false;
            if (_controlledAgents.ContainsKey(agentId)) return false;

            _controlledAgents.Add(agentId, agent);
            _agentToId.Add(agent, agentId);

            return true;
        }

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

        public bool RemoveControlledAgent(Guid agentId)
        {
            if (_controlledAgents.TryGetValue(agentId, out Agent agent))
            {
                return _agentToId.Remove(agent);
            }
            return false;
        }

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

        public void Clear()
        {
            _agentToId.Clear();
            _otherAgents.Clear();
            _agentToId.Clear();
        }
    }
}
