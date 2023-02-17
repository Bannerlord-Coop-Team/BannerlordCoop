using Common.PacketHandlers;
using LiteNetLib;
using Missions.Services.Agents.Packets;
using ProtoBuf;
using SandBox.Missions.MissionLogics;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Represents the delta of an <see cref="Agent"/>'s movement between the time it was created and the time it was last updated.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class AgentMovement : IPacket
    {
        /// <summary>
        /// The <see cref="Vec3"/> representing the direction an <see cref="Agent"/> is looking at.
        /// </summary>
        [ProtoMember(1)]
        public Vec3 LookDirectionDelta = Vec3.Zero;

        /// <summary>
        /// The <see cref="Vec2"/> representing the direction the <see cref="Agent"/> has input for.
        /// </summary>
        [ProtoMember(2)]
        public Vec2 InputDirectionDelta = Vec2.Zero;

        /// <summary>
        /// The <see cref="Agent"/>'s <see cref="AgentActionData"/>.
        /// </summary>
        [ProtoMember(3)]
        public AgentActionData ActionDataDelta;

        /// <summary>
        /// The <see cref="Agent"/>'s <see cref="AgentMountData"/>.
        /// </summary>
        [ProtoMember(4)]
        public AgentMountData MountDataDelta;

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
        /// The <see cref="AgentEquipmentData"/> of the <see cref="Agent"/>.
        /// </summary>
        [ProtoMember(7)]
        public AgentEquipmentData AgentEquipmentData;

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
        /// <param name="agent"></param>
        /// <param name="guid"></param>
        public AgentMovement(
            Vec3 agentCurrentPosition, 
            Vec2 agentMovementDirection, 
            AgentEquipmentData agentEquipmentData, 
            AgentActionData agentActionData,
            AgentMountData agentMountData, 
            Guid guid)
        {
            AgentId = guid;
            CurrentPosition = agentCurrentPosition;
            AgentMovementDirection = agentMovementDirection;
            AgentEquipmentData = agentEquipmentData;
            ActionDataDelta = agentActionData;
            MountDataDelta = agentMountData;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(LookDirectionChanged change)
        {
            LookDirectionDelta = change.LookDirection;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(MovementInputVectorChanged change)
        {
            InputDirectionDelta = change.InputVector;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(ActionDataChanged change)
        {
            ActionDataDelta = change.AgentActionData;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(MountDataChanged change)
        {
            MountDataDelta = change.AgentMountData;
        }

        /// <summary>
        /// Applies the given <see cref="Agent"/> to the <see cref="AgentData"/>
        /// returned by this method.
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public AgentData Apply(Agent agent)
        {
            var agentData = new AgentData(CurrentPosition, AgentMovementDirection, LookDirectionDelta, InputDirectionDelta, AgentEquipmentData, null, null);
            agentData.Apply(agent);
            return agentData;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;

            return Equals((AgentMovement)obj);
        }

        /// <summary>
        /// Compares <paramref name="other"/> with this instance of <see cref="AgentMovement"/>.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(AgentMovement other)
        {
            return other == this 
                || other.AgentId == AgentId 
                && other.LookDirectionDelta == LookDirectionDelta
                && other.InputDirectionDelta == InputDirectionDelta
                && other.ActionDataDelta != null ? other.ActionDataDelta.Equals(ActionDataDelta) : ActionDataDelta == null
                && other.MountDataDelta != null ? other.MountDataDelta.Equals(MountDataDelta) : MountDataDelta == null
                && other.CurrentPosition == CurrentPosition
                && other.AgentMovementDirection == AgentMovementDirection
                && other.AgentEquipmentData.Equals(AgentEquipmentData);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return AgentId.GetHashCode() 
                ^ LookDirectionDelta.GetHashCode() 
                ^ InputDirectionDelta.GetHashCode() 
                ^ ActionDataDelta?.GetHashCode() ?? 37
                ^ MountDataDelta?.GetHashCode() ?? 37
                ^ CurrentPosition.GetHashCode() 
                ^ AgentMovementDirection.GetHashCode() 
                ^ AgentEquipmentData.GetHashCode();
        }
    }
}
