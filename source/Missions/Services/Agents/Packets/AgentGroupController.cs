﻿using Common.Logging;
using Missions.Services.Agents.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    /// <summary>
    /// Agent Grouping Controller for agents controlled by a connected peer
    /// </summary>
    public class AgentGroupController
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AgentGroupController>();

        public IReadOnlyDictionary<Guid, Agent> ControlledAgents => m_ControlledAgents;
        public NetPeer ControllingPeer { get; }

        public AgentGroupController(NetPeer controllingPeer)
        {
            ControllingPeer = controllingPeer;
        }

        private readonly Dictionary<Guid, Agent> m_ControlledAgents = new Dictionary<Guid, Agent>();
        
        public bool Contains(Agent agent)
        {
            return m_ControlledAgents.Values.Contains(agent);
        }

        public bool Contains(Guid agentId)
        {
            return m_ControlledAgents.ContainsKey(agentId);
        }

        public void AddAgent(Guid agentId, Agent agent)
        {
            m_ControlledAgents.Add(agentId, agent);
        }

        public Agent RemoveAgent(Guid agentId)
        {
            if (m_ControlledAgents.TryGetValue(agentId, out Agent agent))
            {
                m_ControlledAgents.Remove(agentId);
                return agent;
            }
            return null;
        }

        public void ApplyMovement(AgentMovement movement)
        {
            if (m_ControlledAgents.TryGetValue(movement.AgentId, out Agent agent))
            {
                movement.Apply(agent);
            }
            else
            {
                Logger.Warning($"{movement.AgentId} has not been registered as a controlled agent");
            }
        }
    }
}
