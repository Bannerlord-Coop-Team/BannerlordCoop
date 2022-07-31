using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions.Packets.Agents
{
    [ProtoContract(SkipConstructor = true)]
    public struct AgentData
    {
        public AgentData(Agent agent)
        {
            Position = agent.Position;
            MovementDirection = agent.GetMovementDirection();
            LookDirection = agent.LookDirection;
            InputVector = agent.MovementInputVector;

            AgentEquipment = new AgentEquipmentData(agent);

            if (agent.Health > 0f)
            {
                ActionData = new AgentActionData(agent);
            }
            else
            {
                ActionData = null;
            }

            if (agent.HasMount)
            {
                MountData = new AgentMountData(agent.MountAgent);
            }
            else
            {
                MountData = null;
            }
        }

        public void Apply(Agent agent)
        {
            // if the player is dead, dont sync anything
            if (agent.Health <= 0)
            {
                return;
            }

            Vec3 pos = Position;

            // if the distance between the local agent and the info passed from the server is greater than 1 unit, teleport the agent
            if (agent.GetPathDistanceToPoint(ref pos) > 1f)
            {
                agent.TeleportToPosition(pos);
            }

            agent.SetMovementDirection(MovementDirection);

            // apply the agent's look direction
            agent.LookDirection = LookDirection;

            // apply the agent's movement input vector...Is this necessary?
            agent.MovementInputVector = InputVector;

            // Update equipment
            AgentEquipment.Apply(agent);

            // Update actions
            ActionData?.Apply(agent);

            // Update mount
            if (agent.HasMount)
            {
                MountData?.ApplyMount(agent.MountAgent);
            }
        }

        [ProtoMember(1)]
        public Vec3 Position { get; }
        [ProtoMember(2)]
        public Vec2 InputVector { get; }
        [ProtoMember(3)]
        public Vec3 LookDirection { get; }
        [ProtoMember(4)]
        public Vec2 MovementDirection { get; }
        [ProtoMember(5)]
        public AgentEquipmentData AgentEquipment { get; }
        [ProtoMember(6)]
        public AgentActionData ActionData { get; }
        [ProtoMember(7)]
        public AgentMountData MountData { get; }
    }
}
