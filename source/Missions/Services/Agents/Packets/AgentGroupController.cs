using Common;
using Common.Logging;
using Common.Util;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    /// <summary>
    /// Agent Grouping Controller for agents controlled by a connected peer
    /// </summary>
    public class AgentGroupController
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AgentGroupController>();

        public IReadOnlyDictionary<string, Agent> ControlledAgents => m_ControlledAgents;
        public NetPeer ControllingPeer { get; }

        public AgentGroupController(NetPeer controllingPeer)
        {
            ControllingPeer = controllingPeer;
        }

        private readonly Dictionary<string, Agent> m_ControlledAgents = new Dictionary<string, Agent>();

        public bool Contains(Agent agent)
        {
            return m_ControlledAgents.Values.Contains(agent);
        }

        public bool Contains(string controllerId)
        {
            return m_ControlledAgents.ContainsKey(controllerId);
        }

        public void AddAgent(string controllerId, Agent agent)
        {
            m_ControlledAgents.Add(controllerId, agent);
        }

        public Agent RemoveAgent(string controllerId)
        {
            if (m_ControlledAgents.TryGetValue(controllerId, out Agent agent))
            {
                m_ControlledAgents.Remove(controllerId);
                return agent;
            }
            return null;
        }

        public void ApplyMovement(MovementPacket movement)
        {
            if (m_ControlledAgents.TryGetValue(movement.AgentId, out Agent agent))
            {
                GameThread.Run(() =>
                {
                    // This action is queued from the network thread and runs a frame later. By then the
                    // local player may have left the instance (mission torn down) or moved to a new one,
                    // leaving this agent invalid — applying movement to it crashes. Only apply while the
                    // agent is still active in the current mission.
                    if (Mission.Current == null || agent.Mission != Mission.Current || agent.IsActive() == false)
                        return;

                    using (new AllowedThread())
                    {
                        movement.Apply(agent);
                    }
                });
            }
            else
            {
                Logger.Warning($"{movement.AgentId} has not been registered as a controlled agent");
            }
        }
    }
}
