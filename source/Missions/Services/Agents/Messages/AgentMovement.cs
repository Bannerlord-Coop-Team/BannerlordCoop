using Common.PacketHandlers;
using LiteNetLib;
using Missions.Services.Agents.Packets;
using ProtoBuf;
using SandBox.Missions.MissionLogics;
using System;
using System.Security.AccessControl;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Represents the delta of an <see cref="Agent"/>'s movement between the time it was created and the time it was last updated.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public struct AgentMovement : IPacket
    {
        /// <summary>
        /// The <see cref="Vec3"/> representing the direction an <see cref="Agent"/> is looking at.
        /// </summary>
        [ProtoMember(1)]
        public Vec3 LookDirection;

        /// <summary>
        /// The <see cref="Vec2"/> representing the direction the <see cref="Agent"/> has input for.
        /// </summary>
        [ProtoMember(2)]
        public Vec2 InputDirection;

        /// <summary>
        /// The <see cref="Agent"/>'s <see cref="AgentActionData"/>.
        /// </summary>
        [ProtoMember(3)]
        public AgentActionData ActionData;

        /// <summary>
        /// The <see cref="Agent"/>'s <see cref="AgentMountData"/>.
        /// </summary>
        [ProtoMember(4)]
        public AgentMountData MountData;

        /// <summary>
        /// The <see cref="Vec3"/> representing the <see cref="Agent"/>'s current position.
        /// </summary>
        [ProtoMember(5)]
        public Vec3 CurrentPosition;

        /// <summary>
        /// The <see cref="Vec2"/> representing the <see cref="Agent"/>'s current movement direction.
        /// </summary>
        [ProtoMember(6)]
        public Vec2 AgentMovementDirection;

        /// <summary>
        /// The <see cref="AgentEquipment"/> of the <see cref="Agent"/>.
        /// </summary>
        [ProtoMember(7)]
        public AgentEquipmentData AgentEquipment;

        /// <summary>
        /// Guid of the associated <see cref="Agent"/>.
        /// </summary>
        [ProtoMember(8)]
        public Guid AgentId { get; }

        /// <summary>
        /// The <see cref="PacketType"/>.
        /// </summary>
        public PacketType PacketType => PacketType.Movement;

        /// <summary>
        /// The <see cref="DeliveryMethod"/>.
        /// </summary>
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agentCurrentPosition"></param>
        /// <param name="agentMovementDirection"></param>
        /// <param name="agentEquipmentData"></param>
        /// <param name="agentActionData"></param>
        /// <param name="agentMountData"></param>
        /// <param name="guid"></param>
        public AgentMovement(
            Vec3 agentCurrentPosition, 
            Vec2 agentMovementDirection, 
            AgentEquipmentData agentEquipmentData, 
            Agent agent, 
            Guid guid)
        {
            AgentId = guid;
            CurrentPosition = agentCurrentPosition;
            AgentMovementDirection = agentMovementDirection;
            AgentEquipment = agentEquipmentData;
            InputDirection = default;
            LookDirection = default;

            if (agent.Health > 0f)
            {
                ActionData = new AgentActionData(agent);
            } 
            else
            {
                ActionData = default;
            }

            if (agent.HasMount)
            {
                MountData = new AgentMountData(agent);
            }
            else
            {
                MountData = default;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="guid"></param>
        public AgentMovement(Guid guid)
        {
            AgentId = guid;
            LookDirection = default;
            InputDirection = default;
            CurrentPosition = default;
            AgentMovementDirection = default;
            AgentEquipment = default;
            ActionData = default;
            MountData = default;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(LookDirectionChanged change)
        {
            LookDirection = change.LookDirection;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(MovementInputVectorChanged change)
        {
            InputDirection = change.InputVector;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(ActionDataChanged change)
        {
            ActionData = change.AgentActionData;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(MountDataChanged change)
        {
            MountData = change.AgentMountData;
        }

        /// <summary>
        /// Applies the movement of this <see cref="AgentMovement"/> to the given <paramref name="agent"/>.
        /// returned by this method.
        /// </summary>
        /// <param name="agent"></param>
        public void Apply(Agent agent)
        {
            if (agent.Health <= 0)
            {
                return;
            }

            ApplyCurrentPosition(agent);
            ApplyMovementDirection(agent);
            ApplyLookDirection(agent);
            ApplyMovementInputVector(agent);
            ApplyAgentEquipment(agent);

            ActionData?.Apply(agent);

            if (agent.HasMount)
            {
                MountData?.ApplyMount(agent);
            }
        }

        private void ApplyCurrentPosition(Agent agent)
        {
            if (CurrentPosition != default && agent.GetPathDistanceToPoint(ref CurrentPosition) > 1f)
            {
                agent.TeleportToPosition(CurrentPosition);
            }
        }

        private void ApplyMovementDirection(Agent agent)
        {
            if (AgentMovementDirection != default)
            {
                agent.SetMovementDirection(AgentMovementDirection);
            }
        }

        private void ApplyLookDirection(Agent agent)
        {
            if (LookDirection != default)
            {
                agent.LookDirection = LookDirection;
            }
        }

        private void ApplyMovementInputVector(Agent agent)
        {
            if (InputDirection != default)
            {
                agent.MovementInputVector = InputDirection;
            }
        }

        private void ApplyAgentEquipment(Agent agent)
        {
            if (AgentEquipment != default)
            {
                AgentEquipment.Apply(agent);
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj) || obj.GetType() != GetType()) return false;

            return Equals((AgentMovement)obj);
        }

        /// <summary>
        /// Compares <paramref name="other"/> with this instance of <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(AgentMovement other)
        {
            return other.AgentId == AgentId 
                && other.LookDirection == LookDirection
                && other.InputDirection == InputDirection
                && other.ActionData != null ? other.ActionData.Equals(ActionData) : ActionData == null
                && other.MountData != null ? other.MountData.Equals(MountData) : MountData == null
                && other.CurrentPosition == CurrentPosition
                && other.AgentMovementDirection == AgentMovementDirection
                && other.AgentEquipment.Equals(AgentEquipment);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return AgentId.GetHashCode() 
                ^ LookDirection.GetHashCode() 
                ^ InputDirection.GetHashCode() 
                ^ ActionData?.GetHashCode() ?? 37
                ^ MountData?.GetHashCode() ?? 37
                ^ CurrentPosition.GetHashCode() 
                ^ AgentMovementDirection.GetHashCode() 
                ^ AgentEquipment.GetHashCode();
        }
    }
}
