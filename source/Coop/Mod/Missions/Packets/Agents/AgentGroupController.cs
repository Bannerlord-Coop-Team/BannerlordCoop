using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions.Packets.Agents
{
    public class AgentGroupController
    {
        private readonly Dictionary<Guid, Agent> ControlledAgents = new Dictionary<Guid, Agent>();

        public void AddAgent(Guid agentId, Agent agent)
        {
            ControlledAgents.Add(agentId, agent);
        }

        public bool RemoveAgent(Guid agentId)
        {
            return ControlledAgents.Remove(agentId);
        }

        public void ApplyMovement(MovementPacket movement)
        {
            if (ControlledAgents.TryGetValue(movement.AgentId, out Agent agent))
            {
                movement.Apply(agent);
            }
            else
            {
                throw new InvalidOperationException($"{movement.AgentId} has not been registered as a controlled agent");
            }
        }
    }
}
